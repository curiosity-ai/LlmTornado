using LlmTornado.Agents.Dnd.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

internal class FantasyPlayer
{
    public string Name { get; set; } = string.Empty;
    public string BackStory { get; set; } = string.Empty;

    public FantasyPlayer(string name, string backStory)
    {
        Name = name;
        BackStory = backStory;
    }
}
