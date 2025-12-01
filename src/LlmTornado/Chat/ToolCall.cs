using System.Reflection.Emit;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.ChatFunctions;

/// <summary>
/// Types of tool call callers for programmatic tool calling.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ToolCallCallerTypes
{
    /// <summary>
    /// Tool was called directly by Claude.
    /// </summary>
    [EnumMember(Value = "direct")]
    Direct,
    
    /// <summary>
    /// Tool was called programmatically from code execution.
    /// </summary>
    [EnumMember(Value = "code_execution_20250825")]
    CodeExecution20250825
}

/// <summary>
/// Information about how a tool was invoked (for programmatic tool calling).
/// </summary>
public class ToolCallCaller
{
    /// <summary>
    /// Type of caller: direct or code execution.
    /// </summary>
    public ToolCallCallerTypes Type { get; set; }
    
    /// <summary>
    /// The tool_id of the code execution tool that made the programmatic call.
    /// Only present when Type is CodeExecution.
    /// </summary>
    public string? ToolId { get; set; }
}

/// <summary>
///     An optional class to be used with models that support returning function calls.
/// </summary>
public class ToolCall
{
    [JsonIgnore] 
    private string? JsonEncoded { get; set; }

    /// <summary>
    ///     Index of the tool call.
    /// </summary>
    [JsonProperty("index")]
    public int? Index { get; set; }
    
    /// <summary>
    ///     The ID of the tool.
    /// </summary>
    [JsonProperty("id")]
    public string? Id { get; set; }

    /// <summary>
    ///     The type of the tool. Currently, this should be always "function" or "custom".
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    ///     The underlying function call, if any.
    /// </summary>
    [JsonProperty("function")]
    public FunctionCall? FunctionCall { get; set; }

    /// <summary>
    ///     The underlying custom tool call, if any.
    /// </summary>
    [JsonProperty("custom")]
    public CustomToolCall? CustomCall { get; set; }
    
    /// <summary>
    /// The underlying built-in tool call, if any.
    /// </summary>
    [JsonIgnore]
    public BuiltInToolCall? BuiltInToolCall { get; set; }
    
    /// <summary>
    ///     The thought signature returned by the model, if any.
    /// </summary>
    [JsonIgnore]
    public string? ThoughtSignature { get; set; }
    
    /// <summary>
    /// Information about how the tool was invoked (Anthropic programmatic tool calling).
    /// Null for direct calls, populated when called from code execution.
    /// </summary>
    [JsonIgnore]
    public ToolCallCaller? Caller { get; set; }
    
    /// <summary>
    ///     Gets the json encoded function call, this is cached to avoid serializing the function over and over.
    /// </summary>
    /// <returns></returns>
    public string GetJson()
    {
        return JsonEncoded ??= JsonConvert.SerializeObject(this, EndpointBase.NullSettings);
    }
}