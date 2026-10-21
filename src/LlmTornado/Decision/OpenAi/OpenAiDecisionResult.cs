using System.Collections.Generic;
using System.Linq;
using LlmTornado.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Decision.OpenAi;

/// <summary>Native decision answers in question order, including per-question refusals.</summary>
public class OpenAiDecisionResult : ApiResultBase
{
    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("answers")]
    public List<OpenAiDecisionAnswer> Answers { get; set; } = [];

    [JsonProperty("usage")]
    public ResponseUsage? Usage { get; set; }

    /// <summary>Finds an answer by its question name. Check Type for per-question refusal.</summary>
    public OpenAiDecisionAnswer? GetAnswer(string name) => Answers.FirstOrDefault(x => x.Name == name);
}

/// <summary>A predicate, choice, score, or refusal answer. Only the corresponding fields are populated.</summary>
public class OpenAiDecisionAnswer
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("probability", NullValueHandling = NullValueHandling.Ignore)]
    public double? Probability { get; set; }

    /// <summary>A string or boolean, preserved without string coercion.</summary>
    [JsonProperty("choice", NullValueHandling = NullValueHandling.Ignore)]
    public JValue? Choice { get; set; }

    [JsonProperty("score", NullValueHandling = NullValueHandling.Ignore)]
    public double? Score { get; set; }

    [JsonProperty("confidence", NullValueHandling = NullValueHandling.Ignore)]
    public double? Confidence { get; set; }

    [JsonProperty("probabilities", NullValueHandling = NullValueHandling.Ignore)]
    public List<OpenAiDecisionProbability>? Probabilities { get; set; }

    [JsonIgnore]
    public bool IsRefusal => Type == "refusal";
}

/// <summary>Probability for a string/boolean choice or numeric score level.</summary>
public class OpenAiDecisionProbability
{
    [JsonProperty("value")]
    public JValue? Value { get; set; }

    [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)]
    public string? Label { get; set; }

    [JsonProperty("probability")]
    public double Probability { get; set; }
}
