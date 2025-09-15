using System.Collections.Generic;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Embedding.Models.Google;

/// <summary>
/// Known embedding models from Google.
/// </summary>
public class EmbeddingModelGoogle : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Google;
    
    /// <summary>
    /// Gemini models.
    /// </summary>
    public readonly EmbeddingModelGoogleGemini Gemini = new EmbeddingModelGoogleGemini();
    
    /// <summary>
    /// All known embedding models from Google.
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
    
    static EmbeddingModelGoogle()
    {
        ModelsAll =
        [
            ..EmbeddingModelGoogleGemini.ModelsAll
        ];
        ModelsAll.ForEach(x =>
        {
            AllModelsMap.Add(x.Name);
        });
    }
    
    internal EmbeddingModelGoogle()
    {
        
    }
}