# Quick Start Guide

## Prerequisites
- .NET 8.0 SDK
- OpenAI API Key

## Installation

1. **Clone or navigate to the repository**
   ```bash
   cd src/LlmTornado.Agents.Dnd
   ```

2. **Build the project**
   ```bash
   dotnet build --configuration Release
   ```

3. **Set your API key**
   ```bash
   # Linux/Mac
   export OPENAI_API_KEY="your-key-here"
   
   # Windows PowerShell
   $env:OPENAI_API_KEY="your-key-here"
   
   # Windows CMD
   set OPENAI_API_KEY=your-key-here
   ```

4. **Run the game**
   ```bash
   dotnet run --configuration Release
   ```

## Your First Adventure

1. Select **"1. Start New Adventure"** from the main menu
2. Create your character:
   - Enter a name (e.g., "Aragorn")
   - Choose a class (Warrior, Mage, Rogue, or Cleric)
   - Choose a race (Human, Elf, Dwarf, or Halfling)
   - Select AI companions (0-3)

3. Start playing! Try commands like:
   - `status` - Check your character
   - `inventory` - View your items
   - `talk Bartender` - Speak with NPCs
   - `explore Town Square` - Move to new locations
   - `quit` - Save and exit

## Common Commands

| Command | Example | Description |
|---------|---------|-------------|
| `explore [location]` | `explore Town Square` | Move to a new location |
| `talk [npc]` | `talk Mysterious Stranger` | Speak with an NPC |
| `attack [target]` | `attack Goblin Scout` | Engage in combat |
| `use [item]` | `use Health Potion` | Use an item |
| `inventory` | `inventory` | View your items |
| `status` | `status` | Check character stats |
| `quit` | `quit` | Save and exit game |

## Locations to Explore

- **Tavern** - Starting location, meet the Mysterious Stranger
- **Town Square** - Central hub with multiple exits
- **Market** - Buy weapons and armor
- **Temple** - Heal and rest
- **Forest Path** - Begin your wilderness adventure
- **Dark Forest** - Encounter dangerous creatures
- **Cave** - Find treasure and face the Cave Troll
- And more!

## Save Files

Your game is automatically saved:
- Every 5 turns during play
- When you quit the game

Save location:
- Windows: `%APPDATA%/LlmTornado.Dnd/saves/`
- Linux/Mac: `~/.config/LlmTornado.Dnd/saves/`

## Tips

1. **Talk to NPCs** - They often give quests and valuable information
2. **Check your status** - Keep track of your health and stats
3. **Explore thoroughly** - Items and secrets are hidden throughout the world
4. **Use AI companions** - They act autonomously and can help in combat
5. **Save often** - While the game auto-saves, you can quit anytime to save manually

## Troubleshooting

**"Error: OPENAI_API_KEY environment variable not set"**
- Make sure you've set your API key as shown in step 3 above

**Game won't start**
- Ensure .NET 8.0 SDK is installed: `dotnet --version`
- Try rebuilding: `dotnet clean && dotnet build`

**Save file issues**
- Check the save directory exists and has write permissions
- Try deleting corrupted save files from the saves directory

## Getting Help

See the full documentation:
- `README.md` - Complete feature documentation
- `VISUAL_GUIDE.md` - Visual examples and architecture

Enjoy your adventure! 🎲⚔️🧙‍♂️
