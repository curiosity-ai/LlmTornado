# LlmTornado.Agents.Dnd

An AI-powered Dungeons & Dragons interactive game built using the LlmTornado Agents Orchestration Library.

## Features

### Agent Orchestration
- **Dungeon Master Agent**: AI-powered DM that narrates the story and manages game state
- **NPC Agents**: AI-controlled non-player characters that interact with players
- **Player Agents**: Support for both human and AI-controlled players in the same party

### Game Features
- **Rich Game World**: Explore multiple locations including taverns, forests, caves, and more
- **Character Creation**: Choose from multiple classes (Warrior, Mage, Rogue, Cleric) and races (Human, Elf, Dwarf, Halfling)
- **Inventory System**: Collect items, weapons, and consumables
- **Quest System**: Track your adventures and progress
- **AI Companions**: Play solo or with up to 3 AI-controlled party members

### Persistence
- **Save/Load System**: Your progress is automatically saved every 5 turns
- **Multiple Save Slots**: Manage multiple game sessions
- **Adventure Revisions**: Every generated or edited campaign is stored as an immutable revision so earlier drafts are never lost
- **Full State Persistence**: Locations, items, player stats, and game history are all preserved

### Adventure Revision Storage
Generated adventures live under `Game_Data/GeneratedAdventures/<adventure-name>/revisions/`.
Each run of the generator—or an edit session triggered from the main menu—creates a new folder such as `rev_001`, `rev_002`, etc.
A manifest file `revisions.json` tracks every revision along with timestamps and lineage so you can pick the exact draft to load or edit later.

## Getting Started

### Prerequisites
- .NET 8.0 or later
- OpenAI API key

### Setup

1. Set your OpenAI API key as an environment variable:
```bash
export OPENAI_API_KEY="your-api-key-here"
```

Or the program will prompt you to enter it at startup.

2. Build the project:
```bash
cd src/LlmTornado.Agents.Dnd
dotnet build
```

3. Run the game:
```bash
dotnet run
```

## How to Play

### Main Menu Options
1. **Start New Adventure**: Create a game session from a generated adventure (choose a specific revision)
2. **Load Saved Game**: Continue from an existing session
3. **Generate New Adventure**: Run the AI pipeline to create a brand-new campaign draft
4. **Edit Generated Adventure**: Branch an existing adventure into a new revision and refine it with the editor
5. **Delete Generated Adventure**: Remove an entire adventure (all revisions) or target a specific revision to prune drafts you no longer need
6. **Delete Save File**: Remove a saved play-through
7. **Quit**: Exit the application

### In-Game Commands
- `explore [location]` or `move [location]` - Travel to a new location
- `talk [npc]` - Speak with NPCs
- `attack [target]` - Engage in combat
- `use [item]` - Use an item from your inventory
- `inventory` - View your current items
- `status` - Check your character stats and information
- `quit` - Exit and save the game

### Character Creation
When starting a new game, you'll:
1. Choose a character name
2. Select a class (affects starting stats and abilities):
   - **Warrior**: High strength and constitution, excels in melee combat
   - **Mage**: High intelligence and wisdom, powerful spellcaster
   - **Rogue**: High dexterity and charisma, stealthy and quick
   - **Cleric**: High wisdom and constitution, healer and support
3. Select a race (provides stat bonuses):
   - **Human**: +2 Charisma
   - **Elf**: +2 Dexterity, +1 Intelligence
   - **Dwarf**: +2 Constitution, +1 Strength
   - **Halfling**: +2 Dexterity, +1 Charisma
4. Choose the number of AI companions (0-3)

## Architecture

### Project Structure
```
LlmTornado.Agents.Dnd/
├── Agents/
│   └── DndGameConfiguration.cs    # Orchestration configuration and runnables
├── DataModels/
│   ├── PlayerCharacter.cs         # Player data model
│   ├── Location.cs                # Location data model
│   ├── Item.cs                    # Item data model
│   ├── GameState.cs               # Complete game state
│   ├── PlayerAction.cs            # Player action definitions
│   ├── DMResponse.cs              # DM response structure
│   └── CharacterCreation.cs       # Character setup data
├── Game/
│   └── GameWorldInitializer.cs    # World setup and character creation
├── Persistence/
│   └── GameStatePersistence.cs    # Save/load functionality
└── Program.cs                     # Main entry point
```

### Agent Flow
1. **DungeonMasterRunnable**: Narrates the current scene based on game state
2. **PlayerActionRunnable**: Captures human player input
3. **NPCActionRunnable**: Processes AI companion actions
4. **GameUpdateRunnable**: Updates game state and determines if game continues
5. Loop back to DungeonMaster or exit

### Orchestration Pattern
The game uses the LlmTornado orchestration framework with:
- **OrchestrationRuntimeConfiguration**: Main game configuration
- **OrchestrationRunnable**: Individual game state processors
- **Advancers**: Define transitions between game states
- **ChatRuntime**: Manages the overall game loop

## Save Files

Save files are stored in:
- Windows: `%APPDATA%/LlmTornado.Dnd/saves/`
- Linux/Mac: `~/.config/LlmTornado.Dnd/saves/`

Each save is named `save_{sessionId}.json` and contains the complete game state.

## Extending the Game

### Adding New Locations
Edit `GameWorldInitializer.cs` and add new entries to the `locations` dictionary.

### Adding New Items
Create new `Item` objects with appropriate properties in location definitions.

### Adding New AI Behaviors
Modify the `NPCActionRunnable` to customize AI decision-making logic.

### Custom Game Mechanics
Extend the `GameUpdateRunnable` to implement new game rules and mechanics.

## Examples

### Starting a New Game
```
Select option: 1
Enter your character name: Aragorn
Choose your class: 1 (Warrior)
Choose your race: 1 (Human)
How many AI companions do you want? (0-3): 2
```

### Exploring the World
```
Aragorn, what do you do?
> explore Town Square

[DM narrates the new location and describes what you see]
```

### Checking Status
```
> status

Aragorn - Warrior Human
Level: 1 | HP: 100/100
Gold: 50 | XP: 0
Stats: Strength:15, Dexterity:10, Constitution:14, Intelligence:10, Wisdom:10, Charisma:12
```

## License

This project follows the same license as the main LlmTornado repository.

## Contributing

Contributions are welcome! Feel free to add new features, locations, items, or improve the game mechanics.
