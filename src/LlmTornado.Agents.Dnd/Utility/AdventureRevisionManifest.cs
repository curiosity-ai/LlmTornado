using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmTornado.Agents.Dnd.Utility;

internal sealed class AdventureRevisionManifest
{
    [JsonPropertyName("adventureTitle")]
    public string AdventureTitle { get; set; } = "";

    [JsonPropertyName("latestRevisionId")]
    public string LatestRevisionId { get; set; } = "";

    [JsonPropertyName("revisions")]
    public List<AdventureRevisionEntry> Revisions { get; set; } = new();

    public AdventureRevisionEntry? GetRevision(string revisionId)
    {
        return Revisions.FirstOrDefault(r => string.Equals(r.RevisionId, revisionId, StringComparison.OrdinalIgnoreCase));
    }

    public AdventureRevisionEntry AddRevision(string? sourceRevisionId = null, string? label = null)
    {
        var nextRevisionNumber = GetNextRevisionNumber();
        var revisionId = $"rev_{nextRevisionNumber:D3}";

        var entry = new AdventureRevisionEntry
        {
            RevisionId = revisionId,
            Label = label ?? $"Revision {nextRevisionNumber}",
            CreatedAtUtc = DateTime.UtcNow,
            SourceRevisionId = sourceRevisionId
        };

        Revisions.Add(entry);
        LatestRevisionId = revisionId;
        return entry;
    }

    private int GetNextRevisionNumber()
    {
        if (Revisions.Count == 0)
        {
            return 1;
        }

        var max = Revisions
            .Select(static r => ParseRevisionNumber(r.RevisionId))
            .DefaultIfEmpty(0)
            .Max();

        return max + 1;
    }

    private static int ParseRevisionNumber(string revisionId)
    {
        if (revisionId.StartsWith("rev_", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(revisionId.AsSpan(4), out int number))
        {
            return number;
        }

        return 0;
    }

    public void Save(string manifestPath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(manifestPath, json);
    }

    public static AdventureRevisionManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new AdventureRevisionManifest();
        }

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<AdventureRevisionManifest>(json) ?? new AdventureRevisionManifest();
    }
}

internal sealed class AdventureRevisionEntry
{
    [JsonPropertyName("revisionId")]
    public string RevisionId { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("sourceRevisionId")]
    public string? SourceRevisionId { get; set; }
}

