namespace KiteFlow.BuildingBlocks.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "kiteflow.identity";

    public string Audience { get; set; } = "kiteflow.platform";

    public string Key { get; set; } = "kiteflow-local-dev-super-secret-key-change-me";

    public int AccessTokenHours { get; set; } = 8;

    public int RefreshTokenDays { get; set; } = 14;

    public int SessionIdleMinutes { get; set; } = 30;
}
