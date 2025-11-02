# LlmTornado.Agents.Dnd - Visual Guide

## Game Startup Screen

```
╔════════════════════════════════════════════════════════════════════════╗
║        LlmTornado D&D - AI-Powered Dungeon & Dragons Adventure        ║
╚════════════════════════════════════════════════════════════════════════╝

════════════════════════════════════════════════════════════════════════
Main Menu:
  1. Start New Adventure
  2. Load Saved Game
  3. List Saved Games
  4. Exit
════════════════════════════════════════════════════════════════════════
Select option: _
```

## Character Creation Flow

```
════════════════════════════════════════════════════════════════════════
Character Creation
════════════════════════════════════════════════════════════════════════
Enter your character name: Aragorn

Choose your class:
  1. Warrior - Strong melee fighter
  2. Mage - Powerful spellcaster
  3. Rogue - Stealthy and quick
  4. Cleric - Healer and support
Select (1-4): 1

Choose your race:
  1. Human - Versatile and adaptable
  2. Elf - Graceful and intelligent
  3. Dwarf - Sturdy and strong
  4. Halfling - Quick and charming
Select (1-4): 1

How many AI companions do you want? (0-3): 2

AI companion Thorin (Dwarf Warrior) has joined your party!
AI companion Elara (Elf Mage) has joined your party!

Welcome, Aragorn the Human Warrior!
Your adventure begins...
```

## In-Game Experience

```
================================================================================
You stand in the Tavern. A warm, bustling tavern filled with adventurers. 
The smell of roasted meat and ale fills the air. A mysterious hooded figure 
sits in the corner, watching you with keen interest. The bartender polishes 
glasses while chatting with patrons. You can hear laughter and the clink of 
coins changing hands.

Available exits lead to: Town Square, Inn Rooms
NPCs present: Bartender, Mysterious Stranger
================================================================================

Result: The mysterious stranger beckons you over with a gnarled finger...

Aragorn, what do you do?
Commands: [explore/move] [location], [talk] [npc], [attack] [target], [use] [item], [inventory], [status], [quit]
> talk Mysterious Stranger

================================================================================
The hooded figure leans forward, his face still hidden in shadow. "Ah, a brave 
soul," he rasps. "I have a quest for you, if you're willing. Deep in the Dark 
Forest, there's a cave. Inside that cave lies an ancient amulet of great power. 
Retrieve it, and I will reward you handsomely. But beware - the cave is guarded 
by creatures most foul."

He slides a worn map across the table. "This will guide you. Take your companions 
and prove your worth."
================================================================================

Thorin (AI): I examine my axe and nod grimly. "Sounds like a proper adventure!"

Elara (AI): I study the map carefully, noting the magical runes marked along the path.

Aragorn, what do you do?
> inventory

Inventory: Iron Sword, Health Potion, Rations

> status

Aragorn - Warrior Human
Level: 1 | HP: 100/100
Gold: 50 | XP: 0
Stats: Strength:15, Dexterity:10, Constitution:14, Intelligence:10, Wisdom:10, Charisma:12

> explore Town Square
```

## Combat Example

```
================================================================================
You venture deeper into the Dark Forest. Suddenly, three goblin scouts emerge 
from behind the trees! They brandish crude weapons and snarl menacingly. Roll 
for initiative!

Your party: Aragorn (100 HP), Thorin (100 HP), Elara (95 HP)
Enemies: Goblin Scout 1 (30 HP), Goblin Scout 2 (30 HP), Goblin Scout 3 (30 HP)
================================================================================

Aragorn, what do you do?
> attack Goblin Scout 1

You swing your Iron Sword in a powerful arc! [Roll: 18 + 5 = 23]
Critical hit! Your blade finds its mark, dealing 22 damage!
Goblin Scout 1 falls, defeated!

Thorin (AI): I charge at Goblin Scout 2 with my battle axe raised high!
[Roll: 15] Thorin's axe connects, dealing 18 damage! Goblin Scout 2 is wounded!

Elara (AI): I cast Fireball at the remaining goblins!
[Roll: 16] Magical flames engulf the enemies! 15 damage to Goblin Scout 2 and 3!

Goblin Scout 2 has been defeated!
Goblin Scout 3 is badly wounded (15 HP remaining)!
```

## Game Progression

```
[Game auto-saved]

Turn 25 reached! Your party has:
- Explored 8 locations
- Defeated 12 enemies
- Completed 2 quests
- Collected 3 rare items
- Gained 150 XP (Level 2!)

Session ID: abc123-def456-ghi789
```

## Architecture Visualization

```
┌─────────────────────────────────────────────────────────────┐
│                    ChatRuntime (Orchestrator)               │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
         ┌────────────────────────────┐
         │  DungeonMasterRunnable     │
         │  (AI DM - GPT-4o)          │
         │  - Narrates story          │
         │  - Responds to actions     │
         │  - Controls NPCs           │
         └────────────┬───────────────┘
                      │
                      ▼
         ┌────────────────────────────┐
         │  PlayerActionRunnable      │
         │  - Captures user input     │
         │  - Displays game state     │
         │  - Validates commands      │
         └────────────┬───────────────┘
                      │
                      ▼
         ┌────────────────────────────┐
         │  NPCActionRunnable         │
         │  (AI Players - GPT-4o-mini)│
         │  - AI companion actions    │
         │  - Party member behavior   │
         └────────────┬───────────────┘
                      │
                      ▼
         ┌────────────────────────────┐
         │  GameUpdateRunnable        │
         │  - Updates game state      │
         │  - Processes consequences  │
         │  - Auto-saves progress     │
         └────────────┬───────────────┘
                      │
                      ▼ (loop back)
              [Next Turn or Exit]
```

## Data Model Structure

```
GameState
├── Players (List<PlayerCharacter>)
│   ├── Human Player
│   │   ├── Name, Class, Race
│   │   ├── Health, MaxHealth
│   │   ├── Stats (Str, Dex, Con, Int, Wis, Cha)
│   │   ├── Inventory (Items)
│   │   └── Abilities
│   └── AI Players (similar structure)
├── Locations (Dictionary<string, Location>)
│   ├── Tavern
│   │   ├── Description
│   │   ├── Exits
│   │   ├── NPCs
│   │   └── Items
│   ├── Town Square
│   ├── Forest Path
│   ├── Dark Forest
│   └── Cave Interior
├── CurrentLocation (string)
├── QuestLog (List<string>)
├── GameHistory (List<string>)
└── TurnNumber (int)
```

## Save File Example

```json
{
  "SessionId": "abc123-def456-ghi789",
  "CreatedAt": "2025-11-02T16:30:00Z",
  "LastSaved": "2025-11-02T16:45:00Z",
  "CurrentLocationName": "Dark Forest",
  "TurnNumber": 25,
  "Players": [
    {
      "Name": "Aragorn",
      "Class": "Warrior",
      "Race": "Human",
      "Level": 2,
      "Health": 95,
      "MaxHealth": 110,
      "Experience": 150,
      "Gold": 125,
      "Stats": {
        "Strength": 15,
        "Dexterity": 10,
        "Constitution": 14,
        "Intelligence": 10,
        "Wisdom": 10,
        "Charisma": 12
      },
      "Inventory": ["Iron Sword", "Health Potion", "Leather Armor"],
      "Abilities": ["Power Strike"],
      "IsAI": false
    }
  ],
  "QuestLog": [
    "Retrieve the ancient amulet from the cave",
    "Defeat the goblin scouts"
  ],
  "GameHistory": [
    "Turn 1: Started adventure at Tavern",
    "Turn 5: Talked to Mysterious Stranger",
    "Turn 10: Explored Town Square",
    "Turn 15: Defeated goblin scouts in Dark Forest"
  ]
}
```

## Key Features Demonstrated

### 1. **AI Orchestration**
- Multiple AI agents working together
- Dungeon Master agent narrates and controls game
- AI companions act autonomously but cooperatively
- Human player interacts naturally with AI agents

### 2. **State Management**
- Full game state persistence
- Auto-save every 5 turns
- Load/save multiple sessions
- Complete history tracking

### 3. **Rich Game World**
- 11 unique locations to explore
- Multiple NPCs and enemies
- Items, weapons, and equipment
- Quest system and progression

### 4. **Player Agency**
- Choose your own adventure path
- Multiple character classes and races
- Party composition (solo or with AI companions)
- Freedom to explore, fight, talk, or trade

### 5. **Extensibility**
- Easy to add new locations
- Simple to create new items
- Customizable AI behaviors
- Modular agent architecture
