using System.Collections.Generic;
using System.Linq;
using LlmTornado.Chat.Models;
using Newtonsoft.Json;

namespace LlmTornado.Assistants;

/// <summary>
///     A request to create an assistant
/// </summary>
public sealed class CreateAssistantRequest
{
    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="assistant">Fetched/created assistant, all fields from this parameter are copied over to the new request</param>
    /// <param name="model">
    ///     ID of the model to use.
    ///     You can use the List models API to see all of your available models,
    ///     or see our Model overview for descriptions of them.
    /// </param>
    /// <param name="name">
    ///     The name of the assistant.
    ///     The maximum length is 256 characters.
    /// </param>
    /// <param name="description">
    ///     The description of the assistant.
    ///     The maximum length is 512 characters.
    /// </param>
    /// <param name="instructions">
    ///     The system instructions that the assistant uses.
    ///     The maximum length is 256000 characters.
    /// </param>
    /// <param name="tools">
    ///     A list of tool enabled on the assistant.
    ///     There can be a maximum of 128 tools per assistant.
    ///     Tools can be of types 'code_interpreter', 'file_search', or 'function'.
    /// </param>
    /// <param name="toolResources">A set of resources that are used by the assistant's tools.
    ///     The resources are specific to the type of tool.
    ///     For example, the code_interpreter tool requires a list of file IDs,
    ///     while the file_search tool requires a list of vector store IDs.</param>
    /// <param name="metadata">
    ///     Set of 16 key-value pairs that can be attached to an object.
    ///     This can be useful for storing additional information about the object in a structured format.
    ///     Keys can be a maximum of 64 characters long and values can be a maximum of 512 characters long.
    /// </param>
    public CreateAssistantRequest(
        Assistant                            assistant,
        ChatModel?                           model         = null,
        string?                              name          = null,
        string?                              description   = null,
        string?                              instructions  = null,
        IEnumerable<AssistantTool>?          tools         = null,
        ToolResources?                       toolResources = null,
        IReadOnlyDictionary<string, string>? metadata      = null)
        : this(model     ?? assistant.Model, name         ?? assistant.Name, description    ?? assistant.Description,
            instructions ?? assistant.Instructions, tools ?? assistant.Tools, toolResources ?? assistant.ToolResources,
            metadata     ?? assistant.Metadata)
    {
    }

    /// <summary>
    ///     Constructor.
    /// </summary>
    /// <param name="model">
    ///     ID of the model to use.
    ///     You can use the List models API to see all of your available models,
    ///     or see our Model overview for descriptions of them.
    /// </param>
    /// <param name="name">
    ///     The name of the assistant.
    ///     The maximum length is 256 characters.
    /// </param>
    /// <param name="description">
    ///     The description of the assistant.
    ///     The maximum length is 512 characters.
    /// </param>
    /// <param name="instructions">
    ///     The system instructions that the assistant uses.
    ///     The maximum length is 256000 characters.
    /// </param>
    /// <param name="tools">
    ///     A list of tool enabled on the assistant.
    ///     There can be a maximum of 128 tools per assistant.
    ///     Tools can be of types 'code_interpreter', 'file_search', or 'function'.
    /// </param>
    /// <param name="toolResources">
    ///     A set of resources that are used by the assistant's tools.
    ///     The resources are specific to the type of tool.
    ///     For example, the code_interpreter tool requires a list of file IDs,
    ///     while the file_search tool requires a list of vector store IDs.</param>
    /// <param name="metadata">
    ///     Set of 16 key-value pairs that can be attached to an object.
    ///     This can be useful for storing additional information about the object in a structured format.
    ///     Keys can be a maximum of 64 characters long and values can be a maximum of 512 characters long.
    /// </param>
    public CreateAssistantRequest(
        ChatModel?                           model         = null,
        string?                              name          = null,
        string?                              description   = null,
        string?                              instructions  = null,
        IEnumerable<AssistantTool>?          tools         = null,
        ToolResources?                       toolResources = null,
        IReadOnlyDictionary<string, string>? metadata      = null)
    {
        Model         = string.IsNullOrWhiteSpace(model?.Name) ? ChatModel.OpenAi.Gpt35.Turbo : model;
        Name          = name;
        Description   = description;
        Instructions  = instructions;
        Tools         = tools?.ToList();
        ToolResources = toolResources;
        Metadata      = metadata;
    }

    /// <summary>
    ///     ID of the model to use.
    ///     You can use the List models API to see all of your available models,
    ///     or see our Model overview for descriptions of them.
    /// </summary>
    [JsonProperty("model", DefaultValueHandling = DefaultValueHandling.Ignore)]
    [JsonConverter(typeof(ChatModelJsonConverter))]
    public ChatModel Model { get; set; }

    /// <summary>
    ///     The name of the assistant.
    ///     The maximum length is 256 characters.
    /// </summary>
    [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Name { get; set; }

    /// <summary>
    ///     The description of the assistant.
    ///     The maximum length is 512 characters.
    /// </summary>
    [JsonProperty("description", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    ///     The system instructions that the assistant uses.
    ///     The maximum length is 32768 characters.
    /// </summary>
    [JsonProperty("instructions", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? Instructions { get; set; }

    /// <summary>
    ///     A list of tool enabled on the assistant.
    ///     There can be a maximum of 128 tools per assistant.
    ///     Tools can be of types 'code_interpreter', 'file_search', or 'function'.
    /// </summary>
    [JsonProperty("tools", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public IReadOnlyList<AssistantTool>? Tools { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object.
    ///     This can be useful for storing additional information about the object in a structured format.
    ///     Keys can be a maximum of 64 characters long and values can be a maximum of 512 characters long.
    /// </summary>
    [JsonProperty("metadata", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    /// <summary>
    ///     What sampling temperature to use, between 0 and 2.
    ///     Higher values like 0.8 will make the output more random,
    ///     while lower values like 0.2 will make it more focused and deterministic.
    /// </summary>
    [JsonProperty("temperature", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float? Temperature { get; set; }

    /// <summary>
    ///     An alternative to sampling with temperature, called nucleus sampling,
    ///     where the model considers the results of the tokens with top_p probability mass.
    ///     So 0.1 means only the tokens comprising the top 10% probability mass are considered.
    ///     We generally recommend altering this or temperature but not both.
    /// </summary>
    [JsonProperty("top_p", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float? TopP { get; set; }

    /// <summary>
    ///     A set of resources that are used by the assistant's tools.
    ///     The resources are specific to the type of tool.
    ///     For example, the `code_interpreter` tool requires a list of file IDs,
    ///     while the `file_search` tool requires a list of vector store IDs.
    /// </summary>
    [JsonProperty("tool_resources", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ToolResources? ToolResources { get; set; }

    /// <summary>
    ///     Specifies the format that the model must output.
    ///     Compatible with GPT-4, GPT-4 Turbo, and all GPT-3.5 Turbo models since `gpt-3.5-turbo-1106`.
    /// </summary>
    [JsonProperty("response_format", DefaultValueHandling = DefaultValueHandling.Ignore), JsonConverter(typeof(ResponseFormatConverter))]
    public ResponseFormat? ResponseFormat { get; set; }
}