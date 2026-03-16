namespace KiteFlow.BuildingBlocks.MultiTenancy;

public abstract class TenantScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SchoolId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
