using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Images.Models.XAi;

/// <summary>
/// Known image models from xAI.
/// </summary>
public class ImageModelXAi : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.XAi;

    /// <summary>
    /// Grok models.
    /// </summary>
    public readonly ImageModelXAiGrok Grok = new ImageModelXAiGrok();

    /// <summary>
    /// All known image models from OpenAI.
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

    static ImageModelXAi()
    {
        ModelsAll =
        [
            ..ImageModelXAiGrok.ModelsAll
        ];

        ModelsAll.ForEach(x =>
        {
            AllModelsMap.Add(x.Name);
        });
    }

    internal ImageModelXAi()
    {

    }
}