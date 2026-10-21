using Newtonsoft.Json;

namespace LlmTornado.Responses;

/// <summary>Changes reasoning effort between GPT-6 single-agent turns while preserving the request-level cache prefix.</summary>
public class ResponseConfigurationUpdate : ResponseInputItem
{
    public override string Type => "configuration_update";

    [JsonProperty("reasoning")]
    public ResponseConfigurationReasoning Reasoning { get; set; } = new();

    public ResponseConfigurationUpdate() { }

    public ResponseConfigurationUpdate(ResponseReasoningEfforts effort)
    {
        Reasoning.Effort = effort;
    }
}

/// <summary>Configuration updates support reasoning effort only.</summary>
public class ResponseConfigurationReasoning
{
    [JsonProperty("effort")]
    public ResponseReasoningEfforts Effort { get; set; }
}
