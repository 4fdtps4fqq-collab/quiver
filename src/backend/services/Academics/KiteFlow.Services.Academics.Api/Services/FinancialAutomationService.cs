using System.Net.Http.Json;
using KiteFlow.Services.Academics.Api.Domain;

namespace KiteFlow.Services.Academics.Api.Services;

public sealed class FinancialAutomationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly SchoolOperationsSettingsClient _settingsClient;

    public FinancialAutomationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        SchoolOperationsSettingsClient settingsClient)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _settingsClient = settingsClient;
    }

    public async Task SyncEnrollmentRevenueAsync(
        Enrollment enrollment,
        Student student,
        Course course,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsClient.GetAsync(enrollment.SchoolId, cancellationToken);
        var isActive = settings.AutoCreateEnrollmentRevenue &&
            enrollment.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed;

        await PostRevenueAsync(
            enrollment.SchoolId,
            sourceType: 1,
            sourceId: enrollment.Id,
            category: "Matrícula",
            amount: enrollment.CoursePriceSnapshot,
            recognizedAtUtc: enrollment.StartedAtUtc,
            description: $"Matrícula de {student.FullName} no curso {course.Name}",
            isActive,
            cancellationToken);
    }

    public async Task SyncSingleLessonRevenueAsync(
        Lesson lesson,
        Student student,
        Instructor instructor,
        CancellationToken cancellationToken = default)
    {
        if (lesson.Kind != LessonKind.Single || !lesson.SingleLessonPrice.HasValue)
        {
            return;
        }

        var settings = await _settingsClient.GetAsync(lesson.SchoolId, cancellationToken);
        var isBillableStatus =
            lesson.Status == LessonStatus.Realized ||
            (lesson.Status == LessonStatus.NoShow && settings.NoShowChargesSingleLesson);

        await PostRevenueAsync(
            lesson.SchoolId,
            sourceType: 2,
            sourceId: lesson.Id,
            category: "Aula avulsa",
            amount: lesson.SingleLessonPrice.Value,
            recognizedAtUtc: lesson.StartAtUtc,
            description: $"Aula avulsa de {student.FullName} com {instructor.FullName}",
            isActive: settings.AutoCreateSingleLessonRevenue && isBillableStatus,
            cancellationToken);
    }

    public Task RemoveSingleLessonRevenueAsync(Guid schoolId, Guid lessonId, CancellationToken cancellationToken = default)
        => PostRevenueAsync(
            schoolId,
            sourceType: 2,
            sourceId: lessonId,
            category: "Aula avulsa",
            amount: 0m,
            recognizedAtUtc: DateTime.UtcNow,
            description: "Receita automática removida",
            isActive: false,
            cancellationToken);

    public Task RemoveEnrollmentRevenueAsync(Guid schoolId, Guid enrollmentId, CancellationToken cancellationToken = default)
        => PostRevenueAsync(
            schoolId,
            sourceType: 1,
            sourceId: enrollmentId,
            category: "Matrícula",
            amount: 0m,
            recognizedAtUtc: DateTime.UtcNow,
            description: "Receita automática removida",
            isActive: false,
            cancellationToken);

    private async Task PostRevenueAsync(
        Guid schoolId,
        int sourceType,
        Guid sourceId,
        string category,
        decimal amount,
        DateTime recognizedAtUtc,
        string description,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("finance");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/finance/revenues/automation");
        var sharedKey = _configuration["InternalServiceAuth:SharedKey"];
        if (!string.IsNullOrWhiteSpace(sharedKey))
        {
            request.Headers.TryAddWithoutValidation("X-KiteFlow-Internal-Key", sharedKey);
        }

        request.Content = JsonContent.Create(new
        {
            SchoolId = schoolId,
            SourceType = sourceType,
            SourceId = sourceId,
            Category = category,
            Amount = amount,
            RecognizedAtUtc = recognizedAtUtc,
            Description = description,
            IsActive = isActive
        });

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
