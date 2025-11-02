using LlmTornado.Agents.ChatRuntime.RuntimeConfigurations;
using LlmTornado.Agents.Dnd.Agents.Runnables;
using LlmTornado.Agents.Dnd.DataModels;
using LlmTornado.Agents.Dnd.Game;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Agents.Dnd.Agents;

/// <summary>
/// Improved configuration for the D&D game with proper phase management and memory system
/// </summary>
public class ImprovedDndGameConfiguration : OrchestrationRuntimeConfiguration
{
    public GameState GameState { get; set; }
    public TornadoApi Client { get; set; }
    public CombatManager CombatManager { get; set; }
    public Adventure? CurrentAdventure { get; set; }
    
    // Memory and relationship management
    public MemoryManager MemoryManager { get; set; }
    public RelationshipManager RelationshipManager { get; set; }
    public ContextManager ContextManager { get; set; }
    
    private PhaseManagerRunnable phaseManager;
    private AdventuringPhaseRunnable adventuringPhase;
    private CombatPhaseRunnable combatPhase;
    private ImprovedExitRunnable exit;

    public ImprovedDndGameConfiguration(TornadoApi client, GameState gameState, Adventure? adventure = null)
    {
        Options.Debug = true;
        Client = client;
        GameState = gameState;
        CurrentAdventure = adventure;
        CombatManager = new CombatManager(gameState, client);
        RecordSteps = true;

        // Initialize memory and relationship systems
        MemoryManager = new MemoryManager(client, gameState.SessionId);
        RelationshipManager = new RelationshipManager(gameState.SessionId);
        ContextManager = new ContextManager(client, MemoryManager, gameState.SessionId);
        
        // Initialize memory system asynchronously
        if (!gameState.MemorySystemInitialized)
        {
            Task.Run(async () =>
            {
                await MemoryManager.InitializeAsync();
                gameState.MemorySystemInitialized = true;
            }).Wait();
        }
        
        // Load existing relationship and context data if available
        LoadMemoryData(gameState);

        // Create the runnables
        phaseManager = new PhaseManagerRunnable(this, GameState, CombatManager);
        adventuringPhase = new AdventuringPhaseRunnable(Client, this, GameState, CombatManager, adventure);
        combatPhase = new CombatPhaseRunnable(this, GameState, CombatManager);
        exit = new ImprovedExitRunnable(this) { AllowDeadEnd = true };

        // Setup the orchestration flow based on phase
        phaseManager.AddAdvancer(
            result => result.ShouldContinue && result.CurrentPhase == GamePhase.Adventuring,
            adventuringPhase);
        
        phaseManager.AddAdvancer(
            result => result.ShouldContinue && result.CurrentPhase == GamePhase.Combat,
            combatPhase);

        adventuringPhase.AddAdvancer(
            result => result.ShouldContinue,
            (result) => new ChatMessage(ChatMessageRoles.User,"Continue the story"),
            phaseManager);

        adventuringPhase.AddAdvancer(result => !result.ShouldContinue, exit);

        combatPhase.AddAdvancer(
            result => result.ShouldContinue,
            (result) => new ChatMessage(ChatMessageRoles.User, "Continue the story"),
            phaseManager);

        combatPhase.AddAdvancer(result => !result.ShouldContinue, exit);

        // Configure entry and exit points
        SetEntryRunnable(phaseManager);
        SetRunnableWithResult(exit);
    }
    
    /// <summary>
    /// Load memory data from disk
    /// </summary>
    private void LoadMemoryData(GameState gameState)
    {
        string baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LlmTornado.Dnd",
            "memory_data",
            gameState.SessionId
        );
        
        if (Directory.Exists(baseDirectory))
        {
            // Load relationships
            var relationshipsPath = Path.Combine(baseDirectory, "relationships.json");
            if (File.Exists(relationshipsPath))
            {
                RelationshipManager.Load(relationshipsPath);
            }
            
            // Load context
            ContextManager.Load(baseDirectory);
        }
        
        gameState.RelationshipDataPath = baseDirectory;
        gameState.ContextDataPath = baseDirectory;
    }
    
    /// <summary>
    /// Save memory data to disk
    /// </summary>
    public void SaveMemoryData()
    {
        if (string.IsNullOrEmpty(GameState.RelationshipDataPath))
        {
            return;
        }
        
        Directory.CreateDirectory(GameState.RelationshipDataPath);
        
        // Save relationships
        var relationshipsPath = Path.Combine(GameState.RelationshipDataPath, "relationships.json");
        RelationshipManager.Save(relationshipsPath);
        
        // Save context
        ContextManager.Save(GameState.ContextDataPath!);
    }
}
