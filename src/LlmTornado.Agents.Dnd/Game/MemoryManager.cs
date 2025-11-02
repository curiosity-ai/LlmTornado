using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.VectorDatabases;
using LlmTornado.VectorDatabases.Faiss.Integrations;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Embedding;
using LlmTornado.Embedding.Models;
using System.Text.Json;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Manages agent memory using FAISS vector storage for retrieval
/// </summary>
public class MemoryManager
{
    private readonly FaissVectorDatabase _vectorDb;
    private readonly TornadoApi _client;
    private readonly string _sessionId;
    private readonly string _collectionName;
    private const int EmbeddingDimension = 1536; // OpenAI embedding dimension
    private const int MaxContextMessages = 20; // Max messages before compression
    
    public MemoryManager(TornadoApi client, string sessionId)
    {
        _client = client;
        _sessionId = sessionId;
        _collectionName = $"dnd_memory_{sessionId}";
        
        // Initialize FAISS with session-specific directory
        string indexPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LlmTornado.Dnd",
            "faiss_indexes"
        );
        
        _vectorDb = new FaissVectorDatabase(indexPath, EmbeddingDimension);
    }
    
    /// <summary>
    /// Initialize the memory collection
    /// </summary>
    public async Task InitializeAsync()
    {
        await _vectorDb.InitializeCollection(_collectionName);
    }
    
    /// <summary>
    /// Store a memory entry in vector database
    /// </summary>
    public async Task StoreMemoryAsync(MemoryEntry memory)
    {
        if (string.IsNullOrEmpty(memory.SessionId))
        {
            memory.SessionId = _sessionId;
        }
        
        // Generate embedding for the memory content
        var embedding = await GenerateEmbeddingAsync(memory.Content);
        
        // Create metadata for filtering
        var metadata = new Dictionary<string, object>
        {
            ["type"] = memory.Type.ToString(),
            ["importance"] = (int)memory.Importance,
            ["sessionId"] = memory.SessionId,
            ["turnNumber"] = memory.TurnNumber,
            ["timestamp"] = memory.Timestamp.ToString("o"),
            ["entities"] = string.Join(",", memory.InvolvedEntities)
        };
        
        if (!string.IsNullOrEmpty(memory.LocationName))
        {
            metadata["location"] = memory.LocationName;
        }
        
        // Add custom metadata
        foreach (var kvp in memory.Metadata)
        {
            metadata[$"custom_{kvp.Key}"] = kvp.Value;
        }
        
        var document = new VectorDocument(
            memory.Id,
            memory.Content,
            metadata,
            embedding
        );
        
        await _vectorDb.AddDocumentsAsync(new[] { document });
    }
    
    /// <summary>
    /// Retrieve relevant memories based on query
    /// </summary>
    public async Task<List<MemoryEntry>> RetrieveMemoriesAsync(
        string query,
        int topK = 5,
        MemoryType? filterType = null,
        ImportanceLevel? minImportance = null)
    {
        var queryEmbedding = await GenerateEmbeddingAsync(query);
        
        // Build where clause for filtering
        TornadoWhereOperator? where = null;
        if (filterType.HasValue)
        {
            where = TornadoWhereOperator.Equal("type", filterType.Value.ToString());
        }
        
        if (minImportance.HasValue)
        {
            var importanceWhere = TornadoWhereOperator.GreaterThanOrEqual("importance", (int)minImportance.Value);
            where = where == null ? importanceWhere : importanceWhere; // TODO: Add AND support if available
        }
        
        var results = await _vectorDb.QueryByEmbeddingAsync(queryEmbedding, where, topK);
        
        var memories = new List<MemoryEntry>();
        foreach (var doc in results)
        {
            var memory = new MemoryEntry
            {
                Id = doc.Id,
                Content = doc.Content,
                Type = Enum.Parse<MemoryType>(doc.Metadata["type"].ToString()!),
                Importance = (ImportanceLevel)(int)(long)doc.Metadata["importance"],
                SessionId = doc.Metadata["sessionId"].ToString()!,
                TurnNumber = Convert.ToInt32(doc.Metadata["turnNumber"]),
                Timestamp = DateTime.Parse(doc.Metadata["timestamp"].ToString()!),
                InvolvedEntities = doc.Metadata["entities"].ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            };
            
            if (doc.Metadata.ContainsKey("location"))
            {
                memory.LocationName = doc.Metadata["location"].ToString();
            }
            
            memories.Add(memory);
        }
        
        return memories;
    }
    
    /// <summary>
    /// Generate embedding for text using OpenAI
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var result = await _client.Embeddings.CreateEmbedding(
            EmbeddingModel.OpenAi.TextEmbedding3Small,
            text
        );
        
        return result.Data[0].Embedding;
    }
    
    /// <summary>
    /// Get context string from relevant memories
    /// </summary>
    public async Task<string> GetMemoryContextAsync(string query, int maxMemories = 5)
    {
        var memories = await RetrieveMemoriesAsync(query, maxMemories);
        
        if (!memories.Any())
        {
            return "";
        }
        
        var contextParts = new List<string> { "=== Relevant Memories ===" };
        foreach (var memory in memories.OrderByDescending(m => m.Importance).ThenByDescending(m => m.Timestamp))
        {
            contextParts.Add($"[{memory.Type} - Turn {memory.TurnNumber}] {memory.Content}");
        }
        contextParts.Add("=== End of Memories ===\n");
        
        return string.Join("\n", contextParts);
    }
    
    /// <summary>
    /// Clean up memory collection
    /// </summary>
    public void Dispose()
    {
        try
        {
            _vectorDb.DeleteCollection(_collectionName);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// Manages entity relationships and friendliness tracking
/// </summary>
public class RelationshipManager
{
    private readonly Dictionary<string, EntityRelationship> _relationships = new();
    private readonly string _sessionId;
    
    public RelationshipManager(string sessionId)
    {
        _sessionId = sessionId;
    }
    
    /// <summary>
    /// Get or create relationship between two entities
    /// </summary>
    public EntityRelationship GetRelationship(string entity1Name, string entity2Name)
    {
        var key = GetRelationshipKey(entity1Name, entity2Name);
        
        if (!_relationships.ContainsKey(key))
        {
            _relationships[key] = new EntityRelationship
            {
                Entity1Id = Guid.NewGuid().ToString(),
                Entity2Id = Guid.NewGuid().ToString(),
                Entity1Name = entity1Name,
                Entity2Name = entity2Name,
                FriendlinessScore = 0, // Neutral
                TrustLevel = 50 // Neutral
            };
        }
        
        return _relationships[key];
    }
    
    /// <summary>
    /// Update relationship based on interaction
    /// </summary>
    public void UpdateRelationship(
        string entity1Name,
        string entity2Name,
        int friendlinessChange,
        int trustChange = 0,
        string? note = null)
    {
        var relationship = GetRelationship(entity1Name, entity2Name);
        
        // Update scores with bounds checking
        relationship.FriendlinessScore = Math.Clamp(
            relationship.FriendlinessScore + friendlinessChange,
            -100, 100);
        
        relationship.TrustLevel = Math.Clamp(
            relationship.TrustLevel + trustChange,
            0, 100);
        
        relationship.InteractionCount++;
        relationship.LastInteraction = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(note))
        {
            relationship.RelationshipNotes.Add($"[{DateTime.UtcNow:g}] {note}");
        }
        
        // Auto-update tags based on friendliness
        UpdateRelationshipTags(relationship);
    }
    
    /// <summary>
    /// Get relationship summary for agent context
    /// </summary>
    public string GetRelationshipContext(string entityName, List<string> relevantEntities)
    {
        var contextParts = new List<string> { "=== Relationship Status ===" };
        
        foreach (var otherEntity in relevantEntities)
        {
            if (otherEntity == entityName) continue;
            
            var rel = GetRelationship(entityName, otherEntity);
            string status = GetRelationshipStatus(rel.FriendlinessScore);
            contextParts.Add($"{otherEntity}: {status} (Friendliness: {rel.FriendlinessScore}, Trust: {rel.TrustLevel})");
        }
        
        contextParts.Add("=== End of Relationships ===\n");
        return string.Join("\n", contextParts);
    }
    
    /// <summary>
    /// Save relationships to disk
    /// </summary>
    public void Save(string savePath)
    {
        var json = JsonSerializer.Serialize(_relationships, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(savePath, json);
    }
    
    /// <summary>
    /// Load relationships from disk
    /// </summary>
    public void Load(string loadPath)
    {
        if (!File.Exists(loadPath)) return;
        
        var json = File.ReadAllText(loadPath);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, EntityRelationship>>(json);
        
        if (loaded != null)
        {
            _relationships.Clear();
            foreach (var kvp in loaded)
            {
                _relationships[kvp.Key] = kvp.Value;
            }
        }
    }
    
    private string GetRelationshipKey(string entity1, string entity2)
    {
        // Ensure consistent key regardless of order
        var names = new[] { entity1, entity2 }.OrderBy(n => n).ToArray();
        return $"{names[0]}||{names[1]}";
    }
    
    private string GetRelationshipStatus(int friendliness)
    {
        return friendliness switch
        {
            >= 80 => "Close Friend",
            >= 50 => "Friendly",
            >= 20 => "Acquaintance",
            >= -20 => "Neutral",
            >= -50 => "Unfriendly",
            >= -80 => "Hostile",
            _ => "Sworn Enemy"
        };
    }
    
    private void UpdateRelationshipTags(EntityRelationship relationship)
    {
        relationship.Tags.Clear();
        
        if (relationship.FriendlinessScore >= 80)
        {
            relationship.Tags.Add("ally");
            relationship.Tags.Add("friend");
        }
        else if (relationship.FriendlinessScore >= 50)
        {
            relationship.Tags.Add("ally");
            relationship.Tags.Add("friendly");
        }
        else if (relationship.FriendlinessScore >= 20)
        {
            relationship.Tags.Add("acquaintance");
        }
        else if (relationship.FriendlinessScore <= -80)
        {
            relationship.Tags.Add("enemy");
            relationship.Tags.Add("hostile");
        }
        else if (relationship.FriendlinessScore <= -50)
        {
            relationship.Tags.Add("hostile");
        }
        else if (relationship.FriendlinessScore <= -20)
        {
            relationship.Tags.Add("unfriendly");
        }
        else
        {
            relationship.Tags.Add("neutral");
        }
        
        if (relationship.TrustLevel >= 80)
        {
            relationship.Tags.Add("trusted");
        }
        else if (relationship.TrustLevel <= 20)
        {
            relationship.Tags.Add("distrusted");
        }
    }
}
