using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LlmTornado.Batch;

/// <summary>
/// Processing status of a batch.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum BatchStatus
{
    /// <summary>
    /// Unknown status.
    /// </summary>
    Unknown,
    
    /// <summary>
    /// The batch is being validated.
    /// </summary>
    [EnumMember(Value = "validating")]
    Validating,
    
    /// <summary>
    /// The batch is in progress.
    /// </summary>
    [EnumMember(Value = "in_progress")]
    InProgress,
    
    /// <summary>
    /// The batch is being finalized.
    /// </summary>
    [EnumMember(Value = "finalizing")]
    Finalizing,
    
    /// <summary>
    /// The batch has completed successfully.
    /// </summary>
    [EnumMember(Value = "completed")]
    Completed,
    
    /// <summary>
    /// The batch has ended (Anthropic).
    /// </summary>
    [EnumMember(Value = "ended")]
    Ended,
    
    /// <summary>
    /// The batch failed.
    /// </summary>
    [EnumMember(Value = "failed")]
    Failed,
    
    /// <summary>
    /// The batch has expired.
    /// </summary>
    [EnumMember(Value = "expired")]
    Expired,
    
    /// <summary>
    /// Cancellation has been initiated.
    /// </summary>
    [EnumMember(Value = "cancelling")]
    Cancelling,
    
    /// <summary>
    /// The batch has been cancelled.
    /// </summary>
    [EnumMember(Value = "cancelled")]
    Cancelled,
    
    /// <summary>
    /// The batch is being canceled. Anthropic spells it with one "l", unlike <see cref="Cancelling"/>.
    /// </summary>
    [EnumMember(Value = "canceling")]
    Canceling
}
