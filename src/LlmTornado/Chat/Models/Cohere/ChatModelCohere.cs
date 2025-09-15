using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models;

/// <summary>
/// Known chat models from Cohere.
/// </summary>
public class ChatModelCohere : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Cohere;

    /// <summary>
    /// Command models.
    /// </summary>
    public readonly ChatModelCohereCommand Command = new ChatModelCohereCommand();

    /// <summary>
    /// Aya models.
    /// </summary>
    public readonly ChatModelCohereAya Aya = new ChatModelCohereAya();

    /// <summary>
    /// All known chat models from Cohere.
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

    static ChatModelCohere()
    {
        ModelsAll =
        [
            ..ChatModelCohereCommand.ModelsAll,
            ..ChatModelCohereAya.ModelsAll
        ];

        ModelsAll.ForEach(x =>
        {
            AllModelsMap.Add(x.Name);
        });
    }

    internal ChatModelCohere()
    {

    }
}