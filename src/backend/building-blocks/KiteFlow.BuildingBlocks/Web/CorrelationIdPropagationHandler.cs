using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace KiteFlow.BuildingBlocks.Web;

public sealed class CorrelationIdPropagationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdPropagationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            var correlationId = CorrelationIdMiddleware.ResolveCorrelationId(_httpContextAccessor.HttpContext);
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
