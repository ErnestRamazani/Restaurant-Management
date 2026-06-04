using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Contracts.Clients;
using EliteRestaurant.Core.Clients;
using EliteRestaurant.Core.Data;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Orders;
using EliteRestaurant.Core.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Policy = "StaffAny")]
public sealed class ClientsController(AppDbContext db) : ControllerBase
{
    private readonly ClientAccountService _clients = new(db);

    [HttpGet]
    public ActionResult<IReadOnlyList<RestaurantClientListItemDto>> List([FromQuery] bool includeInactive = false)
    {
        try
        {
            try
            {
                _clients.EnsureStaffClientsFromEmployees();
            }
            catch
            {
                // Staff mirror is best-effort; listing regular clients must still work.
            }

            var rows = _clients.ListAll(includeInactive);
            return Ok(rows.Select(MapListItem).ToList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = DescribeDbFailure(ex) });
        }
    }

    [HttpGet("search")]
    public ActionResult<IReadOnlyList<RestaurantClientSearchResultDto>> Search([FromQuery] string? q, [FromQuery] int max = 25)
    {
        try
        {
            _clients.EnsureStaffClientsFromEmployees();
        }
        catch
        {
            // Best-effort staff mirror.
        }

        var rows = _clients.Search(q, Math.Clamp(max, 1, 50));
        return Ok(rows.Select(c => new RestaurantClientSearchResultDto(
            c.Id,
            c.UniqueId,
            c.FullName,
            c.PrimaryPhone,
            c.IsStaffClient,
            c.DebtBalanceUsd)).ToList());
    }

    [HttpGet("{id:int}")]
    public ActionResult<RestaurantClientProfileDto> GetProfile(int id)
    {
        var client = _clients.GetById(id);
        if (client is null)
            return NotFound();

        var orders = db.Orders.AsNoTracking()
            .Where(o => o.RestaurantClientId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .Select(o => new
            {
                o.Id,
                o.UniqueId,
                o.CreatedAt,
                o.Status,
                o.ClientSettlement,
                o.AmountOnAccountUsd,
                o.ClientDebtSettledUsd,
                o.PaymentConfirmedAt
            })
            .ToList();

        var orderDtos = new List<ClientOrderTicketDto>();
        foreach (var o in orders)
        {
            var tracked = db.Orders.Include(x => x.Items).ThenInclude(i => i.Product)
                .AsNoTracking().First(x => x.Id == o.Id);
            var grand = _clients.ComputeOrderGrandTotalUsd(tracked);
            orderDtos.Add(new ClientOrderTicketDto(
                o.Id,
                string.IsNullOrWhiteSpace(o.UniqueId) ? $"#{o.Id:000}" : o.UniqueId,
                o.CreatedAt,
                OrderDisplayStatus.ForOrder(o.Status, o.ClientSettlement, o.AmountOnAccountUsd, o.ClientDebtSettledUsd),
                o.ClientSettlement,
                grand,
                o.AmountOnAccountUsd,
                o.ClientDebtSettledUsd,
                o.PaymentConfirmedAt is not null && ClientSettlement.IsOnAccount(o.ClientSettlement)));
        }

        var ledger = db.ClientDebtLedgerEntries.AsNoTracking()
            .Where(e => e.RestaurantClientId == id)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(200)
            .ToList();
        ledger = ClientAccountService.DedupeLedgerEntriesForDisplay(ledger).Take(100).ToList();

        var ledgerDtos = ledger.Select(e =>
        {
            string? code = null;
            if (e.OrderId is int oid)
            {
                code = db.Orders.AsNoTracking().Where(o => o.Id == oid).Select(o => o.UniqueId).FirstOrDefault();
            }

            return new ClientLedgerEntryDto(
                e.Id,
                e.EntryType,
                e.AmountUsd,
                e.BalanceAfterUsd,
                e.Note,
                e.CreatedAtUtc,
                e.OrderId,
                code);
        }).ToList();

        decimal? staffPct = null;
        if (client.IsStaffClient && client.EmployeeId is int eid)
            staffPct = db.Employees.AsNoTracking().Where(e => e.Id == eid).Select(e => e.StaffMealDiscountPercent).FirstOrDefault();

        var detail = new RestaurantClientDetailDto(
            client.Id,
            client.UniqueId,
            client.FullName,
            client.PrimaryPhone,
            client.Email,
            client.InternalNotes,
            client.DebtBalanceUsd,
            _clients.ComputeSettledRevenueUsd(client.Id),
            _clients.ComputeTotalGeneratedRevenueUsd(client.Id),
            client.IsStaffClient,
            client.EmployeeId,
            staffPct,
            client.IsActive);

        return Ok(new RestaurantClientProfileDto(detail, orderDtos, ledgerDtos));
    }

    [HttpPost]
    [Authorize(Policy = "AdminWrite")]
    public ActionResult<RestaurantClientListItemDto> Create([FromBody] CreateRestaurantClientRequest request)
    {
        try
        {
            var (err, created) = _clients.TryCreateClient(
                request.FullName,
                request.PrimaryPhone,
                request.Email,
                request.InternalNotes);
            if (err is not null)
                return BadRequest(new { message = err });
            if (created is null)
                return StatusCode(500, new { message = "Client was not saved." });

            return Ok(MapListItem(created));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = DescribeDbFailure(ex) });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "AdminWrite")]
    public ActionResult Update(int id, [FromBody] UpdateRestaurantClientRequest request)
    {
        var err = _clients.TryUpdateClient(id, request.FullName, request.PrimaryPhone, request.Email, request.InternalNotes, request.IsActive);
        if (err is not null)
            return BadRequest(new { message = err });
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/settle-debt")]
    public ActionResult<SettleClientDebtResponse> SettleDebt(int id, [FromBody] SettleClientDebtRequest request)
    {
        var employeeId = ResolveEmployeeId();
        var (ok, msg, applied, remaining) = _clients.TrySettleDebt(
            id,
            request.PaymentAmountUsd,
            request.Passcode,
            employeeId,
            request.Note);
        if (!ok)
            return BadRequest(new SettleClientDebtResponse(false, msg, remaining, 0m));
        return Ok(new SettleClientDebtResponse(true, null, remaining, applied));
    }

    [HttpPost("orders/{orderId:int}/link")]
    public ActionResult LinkOrder(int orderId, [FromBody] LinkOrderToClientRequest request)
    {
        var err = _clients.TryLinkOrderToClient(orderId, request.RestaurantClientId);
        if (err is not null)
            return BadRequest(new { message = err });
        return Ok(new { ok = true });
    }

    [HttpGet("orders/{orderId:int}/link-info")]
    public ActionResult<OrderClientLinkDto> GetOrderLinkInfo(int orderId)
    {
        var order = db.Orders.AsNoTracking().FirstOrDefault(o => o.Id == orderId);
        if (order is null)
            return NotFound();

        var cap = _clients.GetDebtCapUsd();
        RestaurantClient? client = null;
        if (order.RestaurantClientId is int cid)
            client = db.RestaurantClients.AsNoTracking().FirstOrDefault(c => c.Id == cid);

        var canDebt = client is not null && client.DebtBalanceUsd < cap;
        return Ok(new OrderClientLinkDto(
            order.RestaurantClientId,
            client?.UniqueId,
            client?.FullName,
            client?.DebtBalanceUsd ?? 0m,
            canDebt,
            cap));
    }

    private int? ResolveEmployeeId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static string DescribeDbFailure(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            var msg = cur.Message;
            if (msg.Contains("42703", StringComparison.Ordinal) && msg.Contains("Name", StringComparison.OrdinalIgnoreCase))
                return "Database schema is out of date. Stop the API, run: dotnet ef database update --project EliteRestaurant.Core --startup-project EliteRestaurant.Api";
            if (msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                && msg.Contains("column", StringComparison.OrdinalIgnoreCase))
                return "Database schema is out of date. Run EF migrations on the API database, then restart the API.";
            if (msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                return "A client with this name or phone already exists.";
            if (cur.GetType().FullName == "Npgsql.PostgresException")
            {
                var text = cur.GetType().GetProperty("MessageText")?.GetValue(cur) as string;
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return ex.GetBaseException().Message;
    }

    private static RestaurantClientListItemDto MapListItem(RestaurantClient c) =>
        new(
            c.Id,
            c.UniqueId,
            c.FullName,
            c.PrimaryPhone,
            c.Email,
            c.DebtBalanceUsd,
            c.IsStaffClient,
            c.EmployeeId,
            c.IsActive);
}
