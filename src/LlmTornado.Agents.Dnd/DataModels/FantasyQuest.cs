using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyQuest
{

    // Title of the quest
    public string Title { get; set; }

    //What the quest is about
    public string Description { get; set; }

    //Objective to complete the quest
    public string Objective { get; set; }

    public FantasyLocation StartLocation { get; set; }
}
