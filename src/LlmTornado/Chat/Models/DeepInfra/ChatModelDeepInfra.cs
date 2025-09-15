using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models.DeepInfra;

/// <summary>
/// Known chat models from DeepInfra.
/// </summary>
public class ChatModelDeepInfra : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.DeepInfra;

    /// <summary>
    /// Models from DeepSeek.
    /// </summary>
    public readonly ChatModelDeepInfraDeepSeek DeepSeek = new ChatModelDeepInfraDeepSeek();

    /// <summary>
    /// Models from Qwen.
    /// </summary>
    public readonly ChatModelDeepInfraQwen Qwen = new ChatModelDeepInfraQwen();

    /// <summary>
    /// Models from Meta.
    /// </summary>
    public readonly ChatModelDeepInfraMeta Meta = new ChatModelDeepInfraMeta();

    /// <summary>
    /// Models from Microsoft.
    /// </summary>
    public readonly ChatModelDeepInfraMicrosoft Microsoft = new ChatModelDeepInfraMicrosoft();

    /// <summary>
    /// Models from Google.
    /// </summary>
    public readonly ChatModelDeepInfraGoogle Google = new ChatModelDeepInfraGoogle();

    /// <summary>
    /// All known chat models from DeepInfra.
    /// </summary>
    public override List<IModel> AllModels => ModelsAll;

    /// <summary>
    /// Checks whether the model is owned by the provider.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public override bool OwnsModel(string model)
    {
        return AllModelsMap.Contains(model);
    }

    /// <summary>
    /// Map of models owned by the provider.
    /// </summary>
    public static readonly HashSet<string> AllModelsMap = [];

    /// <summary>
    /// <inheritdoc cref="AllModels"/>
    /// </summary>
    public static readonly List<IModel> ModelsAll;

    static ChatModelDeepInfra()
    {
        ModelsAll =
        [
            ..ChatModelDeepInfraDeepSeek.ModelsAll,
            ..ChatModelDeepInfraQwen.ModelsAll,
        ];

        ModelsAll.ForEach(x =>
        {
            AllModelsMap.Add(x.Name);
        });
    }

    internal ChatModelDeepInfra()
    {

    }
}