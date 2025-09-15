using System.Collections.Generic;
using LlmTornado.Audio.Models.OpenAi;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Audio.Models.Groq;

/// <summary>
/// Known chat models provided by Groq.
/// </summary>
public class AudioModelGroq : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Groq;

    /// <summary>
    /// Models by OpenAI.
    /// </summary>
    public readonly AudioModelGroqOpenAi OpenAi = new AudioModelGroqOpenAi();

    /// <summary>
    /// All known chat models hosted by Groq.
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

    static AudioModelGroq()
    {
        ModelsAll =
        [
            ..AudioModelGroqOpenAi.ModelsAll
        ];

        ModelsAll.ForEach(x =>
        {
            AllModelsMap.Add(x.Name);
        });
    }

    internal AudioModelGroq()
    {

    }
}