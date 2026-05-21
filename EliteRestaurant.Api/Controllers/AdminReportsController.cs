using EliteRestaurant.Contracts.Admin;
using EliteRestaurant.Core.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EliteRestaurant.Api.Controllers;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = "AdminRead")]
public sealed class AdminReportsController(AdminReportAggregationService reports) : ControllerBase
{
    [HttpGet("lists")]
    public async Task<ActionResult<AdminReportListsResponse>> Lists(CancellationToken cancellationToken) =>
        Ok(await reports.GetListsAsync(cancellationToken));

    [HttpGet("daily")]
    public async Task<ActionResult<AdminReportRangeSummaryResponse>> Daily(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reports.GetDailyAsync(start, end, cancellationToken));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Daily report failed.", detail = ex.Message });
        }
    }

    [HttpGet("orders")]
    public async Task<ActionResult<AdminReportRangeSummaryResponse>> Orders(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reports.GetOrdersAsync(start, end, cancellationToken));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Orders report failed.", detail = ex.Message });
        }
    }

    [HttpGet("employee/{employeeId:int}")]
    public async Task<ActionResult<AdminReportEmployeeDetailResponse>> Employee(
        int employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reports.GetEmployeeDetailAsync(employeeId, cancellationToken));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Employee report failed.", detail = ex.Message });
        }
    }

    [HttpGet("table/{tableId:int}")]
    public async Task<ActionResult<AdminReportTableDetailResponse>> Table(
        int tableId,
        CancellationToken cancellationToken) =>
        Ok(await reports.GetTableDetailAsync(tableId, cancellationToken));

    [HttpGet("inventory/{inventoryId:int}")]
    public async Task<ActionResult<AdminReportInventoryDetailResponse>> Inventory(
        int inventoryId,
        CancellationToken cancellationToken) =>
        Ok(await reports.GetInventoryDetailAsync(inventoryId, cancellationToken));

    [HttpGet("menu/{productId:int}")]
    public async Task<ActionResult<AdminReportMenuDetailResponse>> Menu(
        int productId,
        CancellationToken cancellationToken) =>
        Ok(await reports.GetMenuDetailAsync(productId, cancellationToken));

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string type,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(type))
            return BadRequest(new { message = "Query parameter 'type' is required (e.g. Daily, Orders, All Reports)." });

        try
        {
            var (content, fileName, contentType) =
                await reports.ExportAsync(type.Trim(), start, end, cancellationToken);
            return File(content, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
