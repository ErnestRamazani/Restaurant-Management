using EliteRestaurant.Api.Dtos;
using EliteRestaurant.Api.Security;
using EliteRestaurant.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace EliteRestaurant.Api.Tables;

internal static class StaffTableListQuery
{
    public static IReadOnlyList<TableSummaryDto> ListForSession(AppDbContext db, AuthenticatedStaffSession session)
    {
        var query = db.Tables.AsNoTracking()
            .Include(t => t.AssignedServer)
            .Where(t => t.Status != "Maintenance");

        if (session.Role.Equals("Server", StringComparison.OrdinalIgnoreCase))
            query = query.Where(t => t.AssignedServerId == session.EmployeeId);

        return query
            .OrderBy(t => t.TableNumber)
            .Select(t => new TableSummaryDto(
                t.Id,
                t.UniqueId,
                t.TableNumber,
                t.Name,
                t.Capacity,
                t.Status,
                t.AssignedServerId,
                t.AssignedServer == null ? null : t.AssignedServer.Name))
            .ToList();
    }
}
