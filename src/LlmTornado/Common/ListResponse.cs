using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Common;

/// <summary>
/// List of items with pagination.
/// </summary>
public sealed class ListResponse<T> : IListResponse<T>
{
    /// <summary>
    /// Always "list"
    /// </summary>
    [JsonProperty("object")]
    public string Object { get; set; } = "list";

    /// <summary>
    /// Whether there are more items available.
    /// </summary>
    [JsonProperty("has_more")] 
    public bool HasMore { get; internal set; }

    /// <summary>
    /// The ID of the first item in the list.
    /// </summary>
    [JsonProperty("first_id")] 
    public string FirstId { get; internal set; }

    /// <summary>
    /// The ID of the last item in the list.
    /// </summary>
    [JsonProperty("last_id")] 
    public string LastId { get; internal set; }

    /// <summary>
    /// A list of items used to generate this response.
    /// </summary>
    [JsonProperty("data")] 
    public IReadOnlyList<T> Items { get; internal set; } = [];
    
    /// <summary>
    /// Token for fetching the next page. Pass this to <see cref="ListQuery.PageToken"/> for pagination.
    /// </summary>
    [JsonProperty("next_page_token")]
    public string? NextPageToken { get; internal set; }

    // Managed Agents uses next_page rather than next_page_token. Keep the shared public cursor usable.
    [JsonProperty("next_page")]
    private string? NativeNextPage
    {
        set
        {
            NextPageToken = value;
            HasMore = !string.IsNullOrEmpty(value);
        }
    }
    
    /// <summary>
    /// Creates an empty list response. Used by JSON deserializer.
    /// </summary>
    public ListResponse()
    {
    }
    
    /// <summary>
    /// Creates a list response with the given items and pagination info.
    /// </summary>
    internal ListResponse(IReadOnlyList<T> items, bool hasMore = false, string? firstId = null, string? lastId = null, string? nextPageToken = null)
    {
        Items = items;
        HasMore = hasMore;
        FirstId = firstId!;
        LastId = lastId!;
        NextPageToken = nextPageToken;
    }
}
