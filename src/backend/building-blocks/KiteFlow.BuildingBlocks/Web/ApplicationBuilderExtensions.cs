using Microsoft.AspNetCore.Builder;

namespace KiteFlow.BuildingBlocks.Web;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseKiteFlowDefaults(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseAuthentication();
        app.UseMiddleware<MustChangePasswordMiddleware>();
        app.UseAuthorization();

        return app;
    }
}
