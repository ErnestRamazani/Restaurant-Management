using System.Text.Json;
using EliteRestaurant.Core.Models;
using EliteRestaurant.Core.Sync;
using EliteRestaurant.Core.Utils;

namespace EliteRestaurantPro.Services;

internal static class AttendanceCloudHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static List<CloudSyncOperation> BuildAutoAbsenceUpserts(
        IReadOnlyList<Employee> activeEmployees,
        List<EmployeeAttendance> attendancesInRange,
        DateTime fromDate,
        DateTime throughDate,
        HashSet<DateTime> validatedCalendarDates)
    {
        var ops = new List<CloudSyncOperation>();
        var end = throughDate.Date < DateTime.Today ? throughDate.Date : DateTime.Today.AddDays(-1);
        if (end < fromDate.Date)
            return ops;

        for (var d = fromDate.Date; d <= end; d = d.AddDays(1))
        {
            if (validatedCalendarDates.Contains(d.Date))
                continue;

            foreach (var emp in activeEmployees)
            {
                var shift = AttendanceScheduleHelper.ResolveShiftWindow(
                    emp,
                    d,
                    AttendanceShiftSchedule.FromSettings(SettingsManager.Load().Attendance));
                if (shift.IsOff)
                    continue;

                var (dayStartUtc, dayEndExclusiveUtc) = AttendanceCalendar.DayRangeUtc(d);
                var att = attendancesInRange.FirstOrDefault(a =>
                    a.EmployeeId == emp.Id && a.WorkDate >= dayStartUtc && a.WorkDate < dayEndExclusiveUtc);
                if (att is null)
                {
                    var na = new EmployeeAttendance
                    {
                        EmployeeId = emp.Id,
                        WorkDate = dayStartUtc,
                        IsAbsence = true,
                        ClockInStatus = "Absent"
                    };
                    attendancesInRange.Add(na);
                    ops.Add(ToUpsert(na));
                }
                else if (att.ClockInTime is null && !att.IsAbsence)
                {
                    var updated = CloneAttendance(att);
                    updated.IsAbsence = true;
                    if (string.IsNullOrWhiteSpace(updated.ClockInStatus) ||
                        updated.ClockInStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                        updated.ClockInStatus = "Absent";

                    att.IsAbsence = updated.IsAbsence;
                    att.ClockInStatus = updated.ClockInStatus;
                    ops.Add(ToUpsert(updated));
                }
            }
        }

        return ops;
    }

    public static CloudSyncOperation ToUpsert<T>(T entity) where T : class
    {
        var json = JsonSerializer.Serialize(entity, typeof(T), JsonOptions);
        return new CloudSyncOperation(
            Guid.NewGuid().ToString("N"),
            typeof(T).Name,
            "Upsert",
            json,
            DateTime.UtcNow);
    }

    private static EmployeeAttendance CloneAttendance(EmployeeAttendance a) =>
        new()
        {
            Id = a.Id,
            EmployeeId = a.EmployeeId,
            WorkDate = a.WorkDate,
            ClockInTime = a.ClockInTime,
            ClockOutTime = a.ClockOutTime,
            ClockInStatus = a.ClockInStatus,
            Justification = a.Justification,
            IsAbsence = a.IsAbsence,
            AbsenceJustification = a.AbsenceJustification
        };
}
