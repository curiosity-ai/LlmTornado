using System;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Safety;

/// <summary>Retrieves OpenAI project monitoring alerts with the api.safety.alerts.read permission.</summary>
public class OpenAiSafetyAlertsEndpoint : EndpointBase
{
    internal OpenAiSafetyAlertsEndpoint(TornadoApi api) : base(api) { }
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.SafetyAlerts;

    public async Task<OpenAiSafetyAlert> RetrieveAsync(string alertId, CancellationToken cancellationToken = default)
    {
        HttpCallResult<OpenAiSafetyAlert> result = await RetrieveSafeAsync(alertId, cancellationToken).ConfigureAwait(false);
        if (!result.Ok) throw result.Exception ?? new InvalidOperationException("Safety alert retrieval failed.");
        return result.Data!;
    }

    /// <summary>Preserves HTTP errors including safety_alert_not_found and project access failures.</summary>
    public Task<HttpCallResult<OpenAiSafetyAlert>> RetrieveSafeAsync(string alertId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(alertId)) throw new ArgumentException("A safety alert ID is required.", nameof(alertId));
        IEndpointProvider provider = Api.GetProvider(LLmProviders.OpenAi);
        return HttpGet<OpenAiSafetyAlert>(provider, Endpoint, GetUrl(provider, "/" + Uri.EscapeDataString(alertId)), ct: cancellationToken);
    }
}
