using KiteFlow.Services.Academics.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/system/tenants")]
public sealed class SystemTenantsController : ControllerBase
{
    private readonly AcademicsDbContext _dbContext;

    public SystemTenantsController(AcademicsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpDelete("{schoolId:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid schoolId)
    {
        var notifications = await _dbContext.StudentPortalNotifications.Where(x => x.SchoolId == schoolId).ToListAsync();
        var lessons = await _dbContext.Lessons.Where(x => x.SchoolId == schoolId).ToListAsync();
        var ledger = await _dbContext.EnrollmentBalanceLedger.Where(x => x.SchoolId == schoolId).ToListAsync();
        var enrollments = await _dbContext.Enrollments.Where(x => x.SchoolId == schoolId).ToListAsync();
        var courses = await _dbContext.Courses.Where(x => x.SchoolId == schoolId).ToListAsync();
        var instructors = await _dbContext.Instructors.Where(x => x.SchoolId == schoolId).ToListAsync();
        var students = await _dbContext.Students.Where(x => x.SchoolId == schoolId).ToListAsync();

        if (notifications.Count > 0) _dbContext.StudentPortalNotifications.RemoveRange(notifications);
        if (lessons.Count > 0) _dbContext.Lessons.RemoveRange(lessons);
        if (ledger.Count > 0) _dbContext.EnrollmentBalanceLedger.RemoveRange(ledger);
        if (enrollments.Count > 0) _dbContext.Enrollments.RemoveRange(enrollments);
        if (courses.Count > 0) _dbContext.Courses.RemoveRange(courses);
        if (instructors.Count > 0) _dbContext.Instructors.RemoveRange(instructors);
        if (students.Count > 0) _dbContext.Students.RemoveRange(students);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            deletedAtUtc = DateTime.UtcNow,
            schoolId
        });
    }
}
