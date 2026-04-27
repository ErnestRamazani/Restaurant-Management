namespace EliteRestaurant.Core.Utils;

public enum StaffTabletKind
{
    None,
    Server,
    Cashier,
    KitchenBar
}

/// <summary>Tablet session for floor, register, or kitchen/bar (limited nav until logout).</summary>
public static class AppSession
{
    public static StaffTabletKind TabletKind { get; private set; }

    public static int? StaffEmployeeId { get; private set; }

    public static string StaffEmployeeName { get; private set; } = string.Empty;

    /// <summary>Absolute path to profile photo from Employee.ProfileImagePath at sign-in; empty for full admin login.</summary>
    public static string StaffEmployeeProfileImagePath { get; private set; } = string.Empty;

    /// <summary>Optional match when signing in via Administrator portal (not a staff tablet).</summary>
    public static string AdminLoginDisplayName { get; private set; } = string.Empty;

    public static string AdminLoginProfileImagePath { get; private set; } = string.Empty;

    public static bool IsServerTablet => TabletKind == StaffTabletKind.Server;

    public static bool IsCashierTablet => TabletKind == StaffTabletKind.Cashier;

    public static bool IsKitchenBarTablet => TabletKind == StaffTabletKind.KitchenBar;

    /// <summary>Any tablet that is not full admin (server, cashier, or kitchen/bar).</summary>
    public static bool IsStaffTablet => TabletKind != StaffTabletKind.None;

    public static void BeginServerSession(int employeeId, string employeeName, string? profileImagePath = null) =>
        BeginStaffTabletSession(employeeId, employeeName, StaffTabletKind.Server, profileImagePath);

    public static void BeginCashierSession(int employeeId, string employeeName, string? profileImagePath = null) =>
        BeginStaffTabletSession(employeeId, employeeName, StaffTabletKind.Cashier, profileImagePath);

    public static void BeginKitchenBarSession(int employeeId, string employeeName, string? profileImagePath = null) =>
        BeginStaffTabletSession(employeeId, employeeName, StaffTabletKind.KitchenBar, profileImagePath);

    private static void BeginStaffTabletSession(
        int employeeId,
        string employeeName,
        StaffTabletKind kind,
        string? profileImagePath)
    {
        AdminLoginDisplayName = string.Empty;
        AdminLoginProfileImagePath = string.Empty;
        TabletKind = kind;
        StaffEmployeeId = employeeId;
        StaffEmployeeName = employeeName?.Trim() ?? string.Empty;
        StaffEmployeeProfileImagePath = string.IsNullOrWhiteSpace(profileImagePath)
            ? string.Empty
            : profileImagePath.Trim();
    }

    public static void SetAdminLoginProfile(string? displayName, string? profileImagePath)
    {
        AdminLoginDisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
        AdminLoginProfileImagePath = string.IsNullOrWhiteSpace(profileImagePath) ? string.Empty : profileImagePath.Trim();
    }

    public static void Clear()
    {
        TabletKind = StaffTabletKind.None;
        StaffEmployeeId = null;
        StaffEmployeeName = string.Empty;
        StaffEmployeeProfileImagePath = string.Empty;
        AdminLoginDisplayName = string.Empty;
        AdminLoginProfileImagePath = string.Empty;
    }
}
