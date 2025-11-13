using LlmTornado.Agents.Dnd.DataModels.StructuredOutputs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.DataModels.Entities;

public class FantasyAct
{
    [Description("The title of the act.")]
    public string Title { get; set; }

    [Description("A brief overview of the act.")]
    public string Overview { get; set; }

    [Description("The scenes within the act.")]
    public FantasyScene[] Scenes { get; set; }

    //Full act details
    public override string ToString()
    {
        return @$"
Act Title: {Title}
Overview: {Overview}
Scenes: {string.Join("\n", Scenes.Select(s => s.Title))}";

    }
}
