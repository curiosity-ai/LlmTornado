using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using System.Text.Json;

namespace LlmTornado.Agents.Dnd.Game;

/// <summary>
/// Manages conversation context with compression and summarization
/// </summary>
public class ContextManager
{
    private readonly TornadoApi _client;
    private readonly MemoryManager _memoryManager;
    private readonly string _sessionId;
    private readonly Dictionary<string, List<ChatMessage>> _agentConversations = new();
    private readonly Dictionary<string, ConversationSummary> _summaries = new();
    private const int MaxMessagesBeforeCompression = 20;
    private const int MessagesToKeepAfterCompression = 5;
    
    public ContextManager(TornadoApi client, MemoryManager memoryManager, string sessionId)
    {
        _client = client;
        _memoryManager = memoryManager;
        _sessionId = sessionId;
    }
    
    /// <summary>
    /// Add a message to agent conversation history
    /// </summary>
    public void AddMessage(string agentName, ChatMessage message)
    {
        if (!_agentConversations.ContainsKey(agentName))
        {
            _agentConversations[agentName] = new List<ChatMessage>();
        }
        
        _agentConversations[agentName].Add(message);
        
        // Check if compression is needed
        if (_agentConversations[agentName].Count >= MaxMessagesBeforeCompression)
        {
            Task.Run(() => CompressConversationAsync(agentName)).Wait();
        }
    }
    
    /// <summary>
    /// Get conversation history for an agent
    /// </summary>
    public List<ChatMessage> GetConversationHistory(string agentName, bool includeSummary = true)
    {
        var messages = new List<ChatMessage>();
        
        // Add summary if available
        if (includeSummary && _summaries.ContainsKey(agentName))
        {
            var summary = _summaries[agentName];
            messages.Add(new ChatMessage(
                ChatMessageRoles.Unknown,
                $"""
                Previous Conversation Summary (Last {summary.TurnRange} turns, {summary.MessageCount} messages):
                
                {summary.Summary}
                
                Key Points:
                {string.Join("\n", summary.KeyPoints.Select(kp => $"- {kp}"))}
                
                Important Decisions:
                {string.Join("\n", summary.ImportantDecisions.Select(d => $"- {d}"))}
                """
            ));
        }
        
        // Add current conversation
        if (_agentConversations.ContainsKey(agentName))
        {
            messages.AddRange(_agentConversations[agentName]);
        }
        
        return messages;
    }
    
    /// <summary>
    /// Compress conversation history to maintain context window size
    /// </summary>
    private async Task CompressConversationAsync(string agentName)
    {
        if (!_agentConversations.ContainsKey(agentName) || _agentConversations[agentName].Count < MaxMessagesBeforeCompression)
        {
            return;
        }
        
        var messages = _agentConversations[agentName];
        int messagesToCompress = messages.Count - MessagesToKeepAfterCompression;
        
        if (messagesToCompress <= 0)
        {
            return;
        }
        
        var messagesToSummarize = messages.Take(messagesToCompress).ToList();
        
        // Create conversation text for summarization
        var conversationText = string.Join("\n\n", messagesToSummarize.Select((m, i) => 
            $"[{m.Role}]: {m.Content}"));
        
        // Generate summary using AI
        var summaryPrompt = $"""
            Summarize the following D&D game conversation concisely, focusing on:
            1. Key story events and plot developments
            2. Important character interactions and decisions
            3. Quest progress and objectives
            4. Combat outcomes and significant events
            5. Critical information that affects future gameplay
            
            Provide:
            - A brief summary (2-3 paragraphs)
            - A list of 5-7 key points
            - A list of 3-5 important decisions made
            
            Conversation:
            {conversationText}
            """;
        
        try
        {
            var summaryRequest = new ChatRequest()
            {
                Model = ChatModel.OpenAi.Gpt4.O240806,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatMessageRoles.System, "You are a D&D game assistant that creates concise, informative summaries of game sessions."),
                    new ChatMessage(ChatMessageRoles.User, summaryPrompt)
                },
                Temperature = 0.3,
                MaxTokens = 800
            };
            
            var response = await _client.Chat.CreateChatCompletion(summaryRequest);
            var summaryContent = response.Choices[0].Message.Content ?? "";
            
            // Parse summary (simplified - in production, use structured output)
            var summaryLines = summaryContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            var keyPoints = new List<string>();
            var decisions = new List<string>();
            var summaryTextLines = new List<string>();
            
            bool inKeyPoints = false;
            bool inDecisions = false;
            
            foreach (var line in summaryLines)
            {
                var trimmed = line.Trim();
                
                if (trimmed.ToLower().Contains("key point") || trimmed.ToLower().Contains("key events"))
                {
                    inKeyPoints = true;
                    inDecisions = false;
                    continue;
                }
                else if (trimmed.ToLower().Contains("decision") || trimmed.ToLower().Contains("choices"))
                {
                    inDecisions = true;
                    inKeyPoints = false;
                    continue;
                }
                
                if (inKeyPoints && (trimmed.StartsWith("-") || trimmed.StartsWith("•") || trimmed.StartsWith("*")))
                {
                    keyPoints.Add(trimmed.TrimStart('-', '•', '*').Trim());
                }
                else if (inDecisions && (trimmed.StartsWith("-") || trimmed.StartsWith("•") || trimmed.StartsWith("*")))
                {
                    decisions.Add(trimmed.TrimStart('-', '•', '*').Trim());
                }
                else if (!inKeyPoints && !inDecisions && !string.IsNullOrWhiteSpace(trimmed))
                {
                    summaryTextLines.Add(trimmed);
                }
            }
            
            var summaryText = string.Join(" ", summaryTextLines);
            
            // Store summary
            _summaries[agentName] = new ConversationSummary
            {
                AgentName = agentName,
                SessionId = _sessionId,
                MessageCount = messagesToCompress,
                TurnRange = messagesToCompress,
                Summary = summaryText,
                KeyPoints = keyPoints,
                ImportantDecisions = decisions
            };
            
            // Remove compressed messages, keep recent ones
            _agentConversations[agentName] = messages.Skip(messagesToCompress).ToList();
            
            Console.WriteLine($"🗜️ Compressed {messagesToCompress} messages for {agentName} into summary");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to compress conversation for {agentName}: {ex.Message}");
            // On failure, just trim older messages without summary
            _agentConversations[agentName] = messages.Skip(messagesToCompress / 2).ToList();
        }
    }
    
    /// <summary>
    /// Clear conversation history for an agent
    /// </summary>
    public void ClearHistory(string agentName)
    {
        if (_agentConversations.ContainsKey(agentName))
        {
            _agentConversations[agentName].Clear();
        }
        
        if (_summaries.ContainsKey(agentName))
        {
            _summaries.Remove(agentName);
        }
    }
    
    /// <summary>
    /// Get enriched context for agent including memories and relationships
    /// </summary>
    public async Task<string> GetEnrichedContextAsync(
        string agentName,
        string currentSituation,
        List<string> relevantEntities,
        RelationshipManager relationshipManager)
    {
        var contextParts = new List<string>();
        
        // Add relevant memories
        var memories = await _memoryManager.GetMemoryContextAsync(currentSituation, maxMemories: 5);
        if (!string.IsNullOrEmpty(memories))
        {
            contextParts.Add(memories);
        }
        
        // Add relationship context (skip for DM to maintain neutrality)
        if (agentName != "Dungeon Master" && relevantEntities.Any())
        {
            var relationships = relationshipManager.GetRelationshipContext(agentName, relevantEntities);
            contextParts.Add(relationships);
        }
        
        return string.Join("\n\n", contextParts);
    }
    
    /// <summary>
    /// Save context state
    /// </summary>
    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        
        // Save conversations
        var conversationsPath = Path.Combine(directory, "conversations.json");
        var conversationsJson = JsonSerializer.Serialize(_agentConversations, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(conversationsPath, conversationsJson);
        
        // Save summaries
        var summariesPath = Path.Combine(directory, "summaries.json");
        var summariesJson = JsonSerializer.Serialize(_summaries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(summariesPath, summariesJson);
    }
    
    /// <summary>
    /// Load context state
    /// </summary>
    public void Load(string directory)
    {
        // Load conversations
        var conversationsPath = Path.Combine(directory, "conversations.json");
        if (File.Exists(conversationsPath))
        {
            var json = File.ReadAllText(conversationsPath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, List<ChatMessage>>>(json);
            if (loaded != null)
            {
                _agentConversations.Clear();
                foreach (var kvp in loaded)
                {
                    _agentConversations[kvp.Key] = kvp.Value;
                }
            }
        }
        
        // Load summaries
        var summariesPath = Path.Combine(directory, "summaries.json");
        if (File.Exists(summariesPath))
        {
            var json = File.ReadAllText(summariesPath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, ConversationSummary>>(json);
            if (loaded != null)
            {
                _summaries.Clear();
                foreach (var kvp in loaded)
                {
                    _summaries[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
