using System;
using System.Collections.Generic;
using System.Linq;
using LlmTornado.Responses.Events;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Responses;

/// <summary>Supplies saved client function results to a running multi-agent response.</summary>
public class ResponseInjectRequest
{
    public string ResponseId { get; set; } = string.Empty;
    public List<ResponseInputItem> Input { get; set; } = [];
    public ResponseInjectRequest() { }
    public ResponseInjectRequest(string responseId, IEnumerable<ResponseInputItem> input)
    {
        ResponseId = responseId;
        Input = input.ToList();
    }

    public JObject ToPayload()
    {
        if (string.IsNullOrWhiteSpace(ResponseId) || Input.Count == 0 ||
            Input.Any(x => x is null))
            throw new ArgumentException("An injection requires a response ID and at least one non-null input item.");
        return new JObject
        {
            ["type"] = "response.inject", ["response_id"] = ResponseId,
            ["input"] = JArray.FromObject(Input, Newtonsoft.Json.JsonSerializer.Create(EndpointBase.NullSettings))
        };
    }
}

/// <summary>Response-level acknowledgement counts. Accepted events do not identify an individual injection;
/// use the original input returned in a failure for explicit recovery. No results are automatically retried.</summary>
public class ResponseInjectionState
{
    public string ResponseId { get; internal set; } = string.Empty;
    public int PendingAcknowledgements { get; internal set; }
    public int AcceptedCount { get; internal set; }
    public int UnknownAcknowledgements { get; internal set; }
    public IReadOnlyList<JArray> SubmittedInputs { get; internal set; } = [];
    public IReadOnlyList<ResponseEventInjectFailed> Failures { get; internal set; } = [];
}
