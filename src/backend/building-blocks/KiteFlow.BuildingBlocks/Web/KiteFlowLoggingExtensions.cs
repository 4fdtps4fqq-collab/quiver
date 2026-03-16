using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace KiteFlow.BuildingBlocks.Web;

public static class KiteFlowLoggingExtensions
{
    public static WebApplicationBuilder ConfigureKiteFlowLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss ";
            options.SingleLine = true;
        });
        builder.Logging.AddDebug();

        return builder;
    }
}
