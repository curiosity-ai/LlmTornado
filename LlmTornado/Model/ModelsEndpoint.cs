﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LlmTornado.Code;
using Newtonsoft.Json;

namespace LlmTornado.Models;

/// <summary>
///     The API endpoint for querying available models
/// </summary>
public class ModelsEndpoint : EndpointBase
{
    /// <summary>
    ///     Constructor of the api endpoint.  Rather than instantiating this yourself, access it through an instance of
    ///     <see cref="TornadoApi" /> as <see cref="TornadoApi.Models" />.
    /// </summary>
    /// <param name="api"></param>
    internal ModelsEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <summary>
    ///     The name of the endpoint, which is the final path segment in the API URL.  For example, "models".
    /// </summary>
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Models;

    /// <summary>
    ///     Get details about a particular Model from the API, specifically properties such as <see cref="Model.OwnedBy" /> and
    ///     permissions.
    /// </summary>
    /// <param name="id">The id/name of the model to get more details about</param>
    /// <returns>Asynchronously returns the <see cref="Model" /> with all available properties</returns>
    public async Task<Model> RetrieveModelDetailsAsync(string? id)
    {
        string resultAsString = await HttpGetContent(Api.GetProvider(LLmProviders.OpenAi), Endpoint, $"/{id}");
        Model? model          = JsonConvert.DeserializeObject<Model>(resultAsString);
        return model;
    }

    /// <summary>
    ///     List all models via the API
    /// </summary>
    /// <returns>Asynchronously returns the list of all <see cref="Model" />s</returns>
    public async Task<List<Model>?> GetModelsAsync()
    {
        return (await HttpGet<JsonHelperRoot>(Api.GetProvider(LLmProviders.OpenAi), Endpoint))?.data;
    }

    /// <summary>
    ///     Get details about a particular Model from the API, specifically properties such as <see cref="Model.OwnedBy" /> and
    ///     permissions.
    /// </summary>
    /// <param name="id">The id/name of the model to get more details about</param>
    /// <param name="auth">Obsolete: IGNORED</param>
    /// <returns>Asynchronously returns the <see cref="Model" /> with all available properties</returns>
    [Obsolete("Use the overload without the APIAuthentication parameter instead, as custom auth is no longer used.", false)]
    public async Task<Model> RetrieveModelDetailsAsync(string? id, ApiAuthentication auth = null)
    {
        return await RetrieveModelDetailsAsync(id);
    }

    /// <summary>
    ///     A helper class to deserialize the JSON API responses.  This should not be used directly.
    /// </summary>
    private class JsonHelperRoot : ApiResultBase
    {
        [JsonProperty("data")] public List<Model> data { get; set; }

        [JsonProperty("object")] public string obj { get; set; }
    }
}