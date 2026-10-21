using System.Collections.Generic;
using Newtonsoft.Json;

namespace LlmTornado.Files.Vendors.Google;

internal class VendorGoogleTornadoFilesList
{
    [JsonProperty("files")] 
    public List<VendorGoogleTornadoFileContent> Files { get; set; } = [];
    
    [JsonProperty("nextPageToken")]
    public string? PageToken { get; set; }
}