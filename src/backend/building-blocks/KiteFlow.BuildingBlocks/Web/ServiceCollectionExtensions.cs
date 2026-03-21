using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KiteFlow.BuildingBlocks.Web;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKiteFlowWebInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddHealthChecks();
        services.AddTransient<CorrelationIdPropagationHandler>();

        return services;
    }

    public static IHttpClientBuilder AddKiteFlowDownstreamClient(
        this IServiceCollection services,
        string name,
        IConfiguration configuration,
        string configurationPath,
        int timeoutSeconds = 10)
    {
        var baseAddress = configuration[configurationPath];

        return services
            .AddHttpClient(name, client =>
            {
                if (!string.IsNullOrWhiteSpace(baseAddress))
                {
                    client.BaseAddress = new Uri(baseAddress);
                }

                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddHttpMessageHandler<CorrelationIdPropagationHandler>();
    }
}
