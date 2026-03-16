using System.Net.Http.Headers;
using System.Net.Http.Json;
using KiteFlow.BuildingBlocks.MultiTenancy;
using KiteFlow.Services.Equipment.Api.Data;
using KiteFlow.Services.Equipment.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Equipment.Api.Controllers;

[ApiController]
[Authorize(Policy = "EquipmentAccess")]
[Route("api/v1/lesson-equipment")]
public sealed class LessonEquipmentController : ControllerBase
{
    private readonly EquipmentDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpClientFactory _httpClientFactory;

    public LessonEquipmentController(
        EquipmentDbContext dbContext,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("{lessonId:guid}")]
    public async Task<IActionResult> Get(Guid lessonId)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var checkout = await _dbContext.LessonEquipmentCheckouts
            .Where(x => x.SchoolId == schoolId && x.LessonId == lessonId)
            .Select(x => new
            {
                x.Id,
                x.LessonId,
                x.CheckedOutAtUtc,
                x.CheckedInAtUtc,
                x.NotesBefore,
                x.NotesAfter,
                x.CreatedByUserId,
                x.CheckedInByUserId
            })
            .FirstOrDefaultAsync();

        if (checkout is null)
        {
            return NotFound();
        }

        var items = await _dbContext.LessonEquipmentCheckoutItems
            .Where(x => x.SchoolId == schoolId && x.CheckoutId == checkout.Id)
            .Include(x => x.Equipment)
            .Select(x => new
            {
                x.Id,
                x.EquipmentId,
                equipmentName = x.Equipment!.Name,
                equipmentType = x.Equipment.Type.ToString(),
                conditionBefore = x.ConditionBefore.ToString(),
                conditionAfter = x.ConditionAfter.HasValue ? x.ConditionAfter.Value.ToString() : null,
                x.NotesBefore,
                x.NotesAfter
            })
            .ToListAsync();

        return Ok(new { checkout, items });
    }

    [HttpPost("{lessonId:guid}/checkout")]
    public async Task<IActionResult> Checkout(Guid lessonId, [FromBody] CheckoutRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var userId = _currentUser.UserId ?? Guid.Empty;

        var lesson = await GetLessonSnapshotAsync(lessonId);
        if (lesson is null || lesson.SchoolId != schoolId)
        {
            return BadRequest("LessonId is invalid for the current school.");
        }

        var existing = await _dbContext.LessonEquipmentCheckouts.AnyAsync(x => x.SchoolId == schoolId && x.LessonId == lessonId);
        if (existing)
        {
            return Conflict("This lesson already has an equipment checkout.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Selecione pelo menos um equipamento para o checkout.");
        }

        var equipmentIds = request.Items.Select(x => x.EquipmentId).Distinct().ToList();
        if (equipmentIds.Count != request.Items.Count)
        {
            return BadRequest("Duplicated equipment items are not allowed.");
        }

        var equipment = await _dbContext.EquipmentItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.Id) && x.IsActive)
            .ToListAsync();

        if (equipment.Count != equipmentIds.Count)
        {
            return BadRequest("One or more equipment items are invalid or inactive.");
        }

        var alreadyInUse = await _dbContext.LessonEquipmentCheckoutItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.EquipmentId))
            .Join(_dbContext.LessonEquipmentCheckouts.Where(x => x.SchoolId == schoolId && x.CheckedInAtUtc == null),
                item => item.CheckoutId,
                checkout => checkout.Id,
                (item, _) => item.EquipmentId)
            .Distinct()
            .ToListAsync();

        if (alreadyInUse.Count > 0)
        {
            return Conflict("One or more equipment items are already in an open checkout.");
        }

        var checkout = new LessonEquipmentCheckout
        {
            SchoolId = schoolId,
            LessonId = lessonId,
            CreatedByUserId = userId,
            CheckedOutAtUtc = DateTime.UtcNow,
            NotesBefore = NormalizeNullable(request.NotesBefore)
        };

        _dbContext.LessonEquipmentCheckouts.Add(checkout);

        foreach (var item in request.Items)
        {
            _dbContext.LessonEquipmentCheckoutItems.Add(new LessonEquipmentCheckoutItem
            {
                SchoolId = schoolId,
                CheckoutId = checkout.Id,
                EquipmentId = item.EquipmentId,
                ConditionBefore = item.ConditionBefore,
                NotesBefore = NormalizeNullable(item.NotesBefore)
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { checkoutId = checkout.Id });
    }

    [HttpPost("{lessonId:guid}/checkin")]
    public async Task<IActionResult> Checkin(Guid lessonId, [FromBody] CheckinRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var userId = _currentUser.UserId ?? Guid.Empty;

        var lesson = await GetLessonSnapshotAsync(lessonId);
        if (lesson is null || lesson.SchoolId != schoolId)
        {
            return BadRequest("LessonId is invalid for the current school.");
        }

        var checkout = await _dbContext.LessonEquipmentCheckouts
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.LessonId == lessonId);

        if (checkout is null)
        {
            return NotFound("Checkout not found for this lesson.");
        }

        if (checkout.CheckedInAtUtc.HasValue)
        {
            return Conflict("Checkout already closed.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Selecione pelo menos um equipamento para o check-in.");
        }

        var checkoutItems = await _dbContext.LessonEquipmentCheckoutItems
            .Where(x => x.SchoolId == schoolId && x.CheckoutId == checkout.Id)
            .ToListAsync();

        var requestedIds = request.Items.Select(x => x.EquipmentId).OrderBy(x => x).ToList();
        var checkoutIds = checkoutItems.Select(x => x.EquipmentId).OrderBy(x => x).ToList();
        if (!requestedIds.SequenceEqual(checkoutIds))
        {
            return BadRequest("Checkin must contain the same equipment set used in checkout.");
        }

        var equipment = await _dbContext.EquipmentItems
            .Where(x => x.SchoolId == schoolId && requestedIds.Contains(x.Id))
            .ToListAsync();

        foreach (var item in request.Items)
        {
            var checkoutItem = checkoutItems.First(x => x.EquipmentId == item.EquipmentId);
            var equipmentItem = equipment.First(x => x.Id == item.EquipmentId);

            checkoutItem.ConditionAfter = item.ConditionAfter;
            checkoutItem.NotesAfter = NormalizeNullable(item.NotesAfter);

            equipmentItem.CurrentCondition = item.ConditionAfter;
            equipmentItem.TotalUsageMinutes += lesson.DurationMinutes;

            _dbContext.EquipmentUsageLogs.Add(new EquipmentUsageLog
            {
                SchoolId = schoolId,
                EquipmentId = equipmentItem.Id,
                LessonId = lessonId,
                CheckoutItemId = checkoutItem.Id,
                UsageMinutes = lesson.DurationMinutes,
                ConditionAfter = item.ConditionAfter,
                RecordedAtUtc = DateTime.UtcNow
            });
        }

        checkout.CheckedInAtUtc = DateTime.UtcNow;
        checkout.CheckedInByUserId = userId;
        checkout.NotesAfter = NormalizeNullable(request.NotesAfter);

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private async Task<LessonSnapshot?> GetLessonSnapshotAsync(Guid lessonId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("academics");
            var authorization = Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var header))
            {
                client.DefaultRequestHeaders.Authorization = header;
            }

            var response = await client.GetAsync($"/api/v1/lessons/{lessonId}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LessonSnapshot>();
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record CheckoutRequest(string? NotesBefore, List<CheckoutItemRequest> Items);

    public sealed record CheckoutItemRequest(Guid EquipmentId, EquipmentCondition ConditionBefore, string? NotesBefore);

    public sealed record CheckinRequest(string? NotesAfter, List<CheckinItemRequest> Items);

    public sealed record CheckinItemRequest(Guid EquipmentId, EquipmentCondition ConditionAfter, string? NotesAfter);

    public sealed record LessonSnapshot(
        Guid Id,
        string Kind,
        string Status,
        Guid StudentId,
        string StudentName,
        Guid InstructorId,
        string InstructorName,
        Guid? EnrollmentId,
        decimal? SingleLessonPrice,
        DateTime StartAtUtc,
        int DurationMinutes,
        string? Notes)
    {
        public Guid SchoolId { get; init; }
    }
}
