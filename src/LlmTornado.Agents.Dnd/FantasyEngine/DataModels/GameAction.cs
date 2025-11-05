using LlmTornado.Agents.Dnd.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmTornado.Agents.Dnd.FantasyEngine.DataModels;

public enum GameActionType
{
    AddItem,
    RemoveItem,
    ChangeLocation
}

public class GameAction
{
    public GameActionType ActionType { get; set; }
}

public class AddItemAction : GameAction
{
    public FantasyItem ItemToAdd { get; set; }
    public AddItemAction(FantasyItem item)
    {
        ActionType = GameActionType.AddItem;
        ItemToAdd = item;
    }
}

public class RemoveItemAction : GameAction
{
    public FantasyItem ItemToRemove { get; set; }
    public RemoveItemAction(FantasyItem item)
    {
        ActionType = GameActionType.RemoveItem;
        ItemToRemove = item;
    }
}

public class ChangeLocationAction : GameAction
{
    public FantasyLocation NewLocation { get; set; }
    public ChangeLocationAction(FantasyLocation newLocation)
    {
        ActionType = GameActionType.ChangeLocation;
        NewLocation = newLocation;
    }
}
