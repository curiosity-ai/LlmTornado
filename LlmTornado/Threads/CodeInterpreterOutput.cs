using System;
using System.Collections.Generic;
using LlmTornado.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Threads;

/// <summary>
/// Represents an abstract base class for outputs generated by a code interpreter.
/// </summary>
/// <remarks>
/// This class serves as a foundation for derived types, encapsulating shared properties
/// and behaviors for various code interpreter output formats.
/// </remarks>
public abstract class CodeInterpreterOutput
{
    /// <summary>
    ///     Output type. Can be either 'logs' or 'image'.
    /// </summary>
    [JsonProperty("type")]
    public CodeInterpreterOutputTypes Type { get; private set; }
}
/// <summary>
/// Code interpreter logs output.
/// </summary>
public sealed class CodeInterpreterOutputLogs : CodeInterpreterOutput
{
    /// <summary>
    ///     The text output from the Code Interpreter tool call.
    /// </summary>
    [JsonProperty("logs")]
    public string Logs { get; set; } = null!;
}
/// <summary>
///     Code interpreter image output.
/// </summary>
public sealed class CodeInterpreterOutputImage : CodeInterpreterOutput
{
    /// <summary>
    ///     Code interpreter image output.
    /// </summary>
    [JsonProperty("image")]
    public ImageFile Image { get; set; } = null!;
}
internal class CodeInterpreterOutputListConverter : JsonConverter<IReadOnlyList<CodeInterpreterOutput>>
{
    public override void WriteJson(JsonWriter writer, IReadOnlyList<CodeInterpreterOutput>? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }

    public override IReadOnlyList<CodeInterpreterOutput> ReadJson(JsonReader reader, Type objectType, IReadOnlyList<CodeInterpreterOutput>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JArray jsonArray = JArray.Load(reader);

        List<CodeInterpreterOutput> outputList = [];

        foreach (JToken jsonToken in jsonArray)
        {
            JObject                     jsonObject = (JObject)jsonToken;
            CodeInterpreterOutputTypes? outputType = jsonObject["type"]?.ToObject<CodeInterpreterOutputTypes>();

            CodeInterpreterOutput? output = outputType switch
            {
                CodeInterpreterOutputTypes.Image => jsonObject.ToObject<CodeInterpreterOutputImage>(serializer),
                CodeInterpreterOutputTypes.Logs  => jsonObject.ToObject<CodeInterpreterOutputLogs>(serializer),
                _                                => null
            };

            if (output is not null)
            {
                outputList.Add(output);
            }
        }

        return outputList;
    }
}