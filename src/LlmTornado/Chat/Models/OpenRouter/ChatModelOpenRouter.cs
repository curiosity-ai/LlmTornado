using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models.OpenRouter;

/// <summary>
/// Known chat models from Open Router.
/// </summary>
public class ChatModelOpenRouter : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.OpenRouter;

    /// <summary>
    /// All models.
    /// </summary>
    public readonly ChatModelOpenRouterAll All = new ChatModelOpenRouterAll();

    /// <summary>
    /// Map of models owned by the provider.
    /// </summary>
    public static readonly HashSet<string> AllModelsMap = [];

    /// <summary>
    /// <inheritdoc cref="AllModels"/>
    /// </summary>
    public static readonly List<IModel> ModelsAll;

    /// <summary>
    /// All known chat models from Google.
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

    static ChatModelOpenRouter()
    {
        ModelsAll =
        [
            ..ChatModelOpenRouterAll.ModelsAll
        ];

        ModelsAll.ForEach(x =>
        {
            AllModelsMap.Add(x.Name);
        });
    }

    internal ChatModelOpenRouter()
    {

    }
}