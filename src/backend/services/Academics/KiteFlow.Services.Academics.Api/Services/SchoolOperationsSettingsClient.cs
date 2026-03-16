using System.Net.Http.Json;

namespace KiteFlow.Services.Academics.Api.Services;

public sealed class SchoolOperationsSettingsClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SchoolOperationsSettingsClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<SchoolOperationsSettings> GetAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("schools");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/internal/schools/{schoolId}/operations-settings");
        var sharedKey = _configuration["InternalServiceAuth:SharedKey"];
        if (!string.IsNullOrWhiteSpace(sharedKey))
        {
            request.Headers.TryAddWithoutValidation("X-KiteFlow-Internal-Key", sharedKey);
        }

        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return SchoolOperationsSettings.Default;
        }

        return await response.Content.ReadFromJsonAsync<SchoolOperationsSettings>(cancellationToken: cancellationToken)
            ?? SchoolOperationsSettings.Default;
    }
}

public sealed record SchoolOperationsSettings(
    int BookingLeadTimeMinutes,
    int CancellationWindowHours,
    int RescheduleWindowHours,
    int AttendanceConfirmationLeadMinutes,
    int LessonReminderLeadHours,
    bool PortalNotificationsEnabled,
    string ThemePrimary,
    string ThemeAccent,
    int InstructorBufferMinutes,
    int NoShowGraceMinutes,
    bool NoShowConsumesCourseMinutes,
    bool NoShowChargesSingleLesson,
    bool AutoCreateEnrollmentRevenue,
    bool AutoCreateSingleLessonRevenue)
{
    public static readonly SchoolOperationsSettings Default = new(
        60,
        24,
        24,
        180,
        18,
        true,
        "#0E3A52",
        "#FFB703",
        15,
        15,
        true,
        true,
        true,
        true);
}
