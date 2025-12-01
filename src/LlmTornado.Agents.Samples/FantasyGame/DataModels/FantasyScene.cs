using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using LlmTornado.Agents.Dnd.FantasyEngine.DataModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.DataModels;

public class FantasyScene
{
    public string Title { get; set; }

    public string Overview { get; set; }

    public string Goals { get; set; }

    public FantasyLocation[] Locations{ get; set; }

    public string[] Outcomes { get; set; }

    public string? HazardsAndChallenges { get; set; } = "N/A";

    public string? AdditionSceneSpecificElements { get; set; } = "N/A";

    public FantasyNPC[]? ImportantNPCs { get; set; }

    public override string ToString()
    {
        return @$"
Scene Title: {Title}
Overview: {Overview}
Goals: {Goals}
Outcomes: {string.Join(", ", Outcomes)}
Hazards and Challenges: {HazardsAndChallenges}
Additional Elements: {AdditionSceneSpecificElements}
Important NPCs: {(ImportantNPCs != null ? string.Join(", ", ImportantNPCs.Select(npc => npc.Name)) : "N/A")}";
    }
}

