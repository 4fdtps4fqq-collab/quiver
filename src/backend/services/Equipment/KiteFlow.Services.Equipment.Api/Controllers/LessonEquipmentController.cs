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

        var items = checkout is null
            ? Array.Empty<object>()
            : (await _dbContext.LessonEquipmentCheckoutItems
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
                .ToListAsync()).Cast<object>().ToArray();

        var reservation = await _dbContext.EquipmentReservations
            .Where(x => x.SchoolId == schoolId && x.LessonId == lessonId)
            .Select(x => new
            {
                x.Id,
                x.LessonId,
                x.ReservedFromUtc,
                x.ReservedUntilUtc,
                x.Notes,
                x.CreatedByUserId
            })
            .FirstOrDefaultAsync();

        var reservedItems = reservation is null
            ? Array.Empty<object>()
            : (await _dbContext.EquipmentReservationItems
                .Where(x => x.SchoolId == schoolId && x.ReservationId == reservation.Id)
                .Include(x => x.Equipment)
                .Select(x => new
                {
                    x.Id,
                    x.EquipmentId,
                    equipmentName = x.Equipment!.Name,
                    equipmentType = x.Equipment.Type.ToString()
                })
                .ToListAsync()).Cast<object>().ToArray();

        return Ok(new { checkout, items, reservation, reservedItems });
    }

    [HttpPost("{lessonId:guid}/reserve")]
    public async Task<IActionResult> Reserve(Guid lessonId, [FromBody] ReserveEquipmentRequest request)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;
        var userId = _currentUser.UserId ?? Guid.Empty;

        var lesson = await GetLessonSnapshotAsync(lessonId);
        if (lesson is null || lesson.SchoolId != schoolId)
        {
            return BadRequest("LessonId inválido para a escola atual.");
        }

        var equipmentIds = await ExpandEquipmentIdsAsync(schoolId, request.EquipmentIds, request.KitIds);
        if (equipmentIds.Count == 0)
        {
            return BadRequest("Selecione pelo menos um equipamento ou kit para reservar.");
        }

        var validation = await ValidateEquipmentAvailabilityAsync(schoolId, lessonId, lesson.StartAtUtc, lesson.StartAtUtc.AddMinutes(lesson.DurationMinutes), equipmentIds);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await _dbContext.EquipmentReservations
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.LessonId == lessonId);

        if (existing is not null)
        {
            var existingItems = await _dbContext.EquipmentReservationItems
                .Where(x => x.SchoolId == schoolId && x.ReservationId == existing.Id)
                .ToListAsync();

            _dbContext.EquipmentReservationItems.RemoveRange(existingItems);
            existing.ReservedFromUtc = lesson.StartAtUtc;
            existing.ReservedUntilUtc = lesson.StartAtUtc.AddMinutes(lesson.DurationMinutes);
            existing.Notes = NormalizeNullable(request.Notes);
        }
        else
        {
            existing = new EquipmentReservation
            {
                SchoolId = schoolId,
                LessonId = lessonId,
                ReservedFromUtc = lesson.StartAtUtc,
                ReservedUntilUtc = lesson.StartAtUtc.AddMinutes(lesson.DurationMinutes),
                Notes = NormalizeNullable(request.Notes),
                CreatedByUserId = userId
            };

            _dbContext.EquipmentReservations.Add(existing);
        }

        foreach (var equipmentId in equipmentIds)
        {
            _dbContext.EquipmentReservationItems.Add(new EquipmentReservationItem
            {
                SchoolId = schoolId,
                ReservationId = existing.Id,
                EquipmentId = equipmentId
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { reservationId = existing.Id, reservedItems = equipmentIds.Count });
    }

    [HttpPost("{lessonId:guid}/reservation/release")]
    public async Task<IActionResult> ReleaseReservation(Guid lessonId)
    {
        _currentTenant.EnsureTenant();
        var schoolId = _currentTenant.SchoolId!.Value;

        var reservation = await _dbContext.EquipmentReservations
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.LessonId == lessonId);

        if (reservation is null)
        {
            return Ok(new { released = false });
        }

        var items = await _dbContext.EquipmentReservationItems
            .Where(x => x.SchoolId == schoolId && x.ReservationId == reservation.Id)
            .ToListAsync();

        _dbContext.EquipmentReservationItems.RemoveRange(items);
        _dbContext.EquipmentReservations.Remove(reservation);
        await _dbContext.SaveChangesAsync();

        return Ok(new { released = true });
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
            return BadRequest("LessonId inválido para a escola atual.");
        }

        var existing = await _dbContext.LessonEquipmentCheckouts.AnyAsync(x => x.SchoolId == schoolId && x.LessonId == lessonId);
        if (existing)
        {
            return Conflict("Esta aula já possui checkout de equipamento.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("Selecione pelo menos um equipamento para o checkout.");
        }

        var equipmentIds = request.Items.Select(x => x.EquipmentId).Distinct().ToList();
        if (equipmentIds.Count != request.Items.Count)
        {
            return BadRequest("Não é permitido repetir equipamento no checkout.");
        }

        var validation = await ValidateEquipmentAvailabilityAsync(
            schoolId,
            lessonId,
            lesson.StartAtUtc,
            lesson.StartAtUtc.AddMinutes(lesson.DurationMinutes),
            equipmentIds);

        if (validation is not null)
        {
            return validation;
        }

        var equipment = await _dbContext.EquipmentItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.Id) && x.IsActive)
            .ToListAsync();

        if (equipment.Count != equipmentIds.Count)
        {
            return BadRequest("Um ou mais equipamentos são inválidos ou estão inativos.");
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

        var reservation = await _dbContext.EquipmentReservations
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.LessonId == lessonId);

        if (reservation is not null)
        {
            var reservationItems = await _dbContext.EquipmentReservationItems
                .Where(x => x.SchoolId == schoolId && x.ReservationId == reservation.Id)
                .ToListAsync();

            _dbContext.EquipmentReservationItems.RemoveRange(reservationItems);
            _dbContext.EquipmentReservations.Remove(reservation);
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
            return BadRequest("LessonId inválido para a escola atual.");
        }

        var checkout = await _dbContext.LessonEquipmentCheckouts
            .FirstOrDefaultAsync(x => x.SchoolId == schoolId && x.LessonId == lessonId);

        if (checkout is null)
        {
            return NotFound("Checkout não encontrado para esta aula.");
        }

        if (checkout.CheckedInAtUtc.HasValue)
        {
            return Conflict("Este checkout já foi encerrado.");
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
            return BadRequest("O check-in deve conter exatamente o mesmo conjunto de equipamentos do checkout.");
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

    private async Task<IActionResult?> ValidateEquipmentAvailabilityAsync(
        Guid schoolId,
        Guid lessonId,
        DateTime fromUtc,
        DateTime untilUtc,
        List<Guid> equipmentIds)
    {
        var alreadyInUse = await _dbContext.LessonEquipmentCheckoutItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.EquipmentId))
            .Join(_dbContext.LessonEquipmentCheckouts.Where(x => x.SchoolId == schoolId && x.CheckedInAtUtc == null),
                item => item.CheckoutId,
                checkout => checkout.Id,
                (item, checkout) => new { item.EquipmentId, checkout.LessonId })
            .Where(x => x.LessonId != lessonId)
            .Select(x => x.EquipmentId)
            .Distinct()
            .ToListAsync();

        if (alreadyInUse.Count > 0)
        {
            return Conflict("Um ou mais equipamentos já estão em checkout aberto.");
        }

        var reservedByOtherLesson = await _dbContext.EquipmentReservationItems
            .Where(x => x.SchoolId == schoolId && equipmentIds.Contains(x.EquipmentId))
            .Join(_dbContext.EquipmentReservations.Where(x =>
                    x.SchoolId == schoolId &&
                    x.LessonId != lessonId &&
                    x.ReservedFromUtc < untilUtc &&
                    x.ReservedUntilUtc > fromUtc),
                item => item.ReservationId,
                reservation => reservation.Id,
                (item, _) => item.EquipmentId)
            .Distinct()
            .ToListAsync();

        if (reservedByOtherLesson.Count > 0)
        {
            return Conflict("Um ou mais equipamentos já estão reservados para outra aula neste horário.");
        }

        var blockedByCondition = await _dbContext.EquipmentItems
            .Where(x =>
                x.SchoolId == schoolId &&
                equipmentIds.Contains(x.Id) &&
                (x.CurrentCondition == EquipmentCondition.NeedsRepair ||
                 x.CurrentCondition == EquipmentCondition.OutOfService ||
                 !x.IsActive))
            .Select(x => x.Name)
            .ToListAsync();

        if (blockedByCondition.Count > 0)
        {
            return Conflict($"Os seguintes equipamentos não estão disponíveis para operação: {string.Join(", ", blockedByCondition)}.");
        }

        return null;
    }

    private async Task<List<Guid>> ExpandEquipmentIdsAsync(Guid schoolId, List<Guid>? equipmentIds, List<Guid>? kitIds)
    {
        var ids = new HashSet<Guid>((equipmentIds ?? []).Where(x => x != Guid.Empty));

        var normalizedKitIds = (kitIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
        if (normalizedKitIds.Count > 0)
        {
            var kitEquipmentIds = await _dbContext.EquipmentKitItems
                .Where(x => x.SchoolId == schoolId && normalizedKitIds.Contains(x.KitId))
                .Select(x => x.EquipmentId)
                .ToListAsync();

            foreach (var equipmentId in kitEquipmentIds)
            {
                ids.Add(equipmentId);
            }
        }

        return ids.ToList();
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

    public sealed record ReserveEquipmentRequest(List<Guid>? EquipmentIds, List<Guid>? KitIds, string? Notes);

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
