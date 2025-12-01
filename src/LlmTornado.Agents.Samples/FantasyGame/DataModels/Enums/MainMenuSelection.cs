using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public enum MainMenuSelection
{
    StartNewAdventure,
    GenerateNewAdventure,
    EditGeneratedAdventure,
    LoadSavedGame,
    DeleteAdventure,
    DeleteSaveFile,
    Settings,
    QuitGame,
    InvalidSelection
}
