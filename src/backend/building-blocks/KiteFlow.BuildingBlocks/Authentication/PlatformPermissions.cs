using System.Security.Claims;

namespace KiteFlow.BuildingBlocks.Authentication;

public static class PlatformPermissions
{
    public const string DashboardView = "dashboard.view";
    public const string StudentsManage = "students.manage";
    public const string InstructorsManage = "instructors.manage";
    public const string CoursesManage = "courses.manage";
    public const string EnrollmentsManage = "enrollments.manage";
    public const string LessonsManage = "lessons.manage";
    public const string EquipmentManage = "equipment.manage";
    public const string MaintenanceManage = "maintenance.manage";
    public const string FinanceManage = "finance.manage";
    public const string SchoolManage = "school.manage";

    public static readonly string[] All =
    [
        DashboardView,
        StudentsManage,
        InstructorsManage,
        CoursesManage,
        EnrollmentsManage,
        LessonsManage,
        EquipmentManage,
        MaintenanceManage,
        FinanceManage,
        SchoolManage
    ];

    public static IReadOnlyCollection<string> GetDefaultPermissions(string? role)
        => role switch
        {
            "SystemAdmin" => All,
            "Owner" => All,
            "Admin" =>
            [
                DashboardView,
                StudentsManage,
                InstructorsManage,
                CoursesManage,
                EnrollmentsManage,
                LessonsManage,
                EquipmentManage,
                MaintenanceManage,
                FinanceManage,
                SchoolManage
            ],
            "Instructor" =>
            [
                DashboardView,
                StudentsManage,
                CoursesManage,
                LessonsManage,
                EquipmentManage,
                MaintenanceManage
            ],
            "Student" => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };

    public static IReadOnlyCollection<string> Normalize(IEnumerable<string>? permissions)
        => permissions?
            .Select(static item => item?.Trim())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item!)
            .Where(static item => All.Contains(item, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray()
           ?? Array.Empty<string>();

    public static bool HasPermission(ClaimsPrincipal user, string permission)
        => user.Claims.Any(x =>
            x.Type == "permission" &&
            string.Equals(x.Value, permission, StringComparison.OrdinalIgnoreCase));
}
