using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnitController))]
public class CommandController : MonoBehaviour
{
    private UnitController unit;
    private EntityStats stats;

    void Awake()
    {
        unit = GetComponent<UnitController>();
        stats = GetComponent<EntityStats>();
    }

    // Called by PlayerInputHandler on hotkey — explicit commands only
    public void ExecuteHotkeyCommand(HotkeySlot slot, RaycastHit hit)
    {
        foreach (CommandData cmd in stats.baseData.baseCommands)
        {
            if (cmd.hotkey != slot) continue;

            ExecuteCommand(cmd.commandType);
            
            return;
        }
    }

    public void ExecuteCommand(CommandType cmdType)
    {
        switch (cmdType)
        {
            case CommandType.Stop: unit.OrderStop(); break;
            case CommandType.Build: break;
        }
    }

    // Returns command list for UI button generation
    public List<CommandData> GetAllCommands()
    {
        return stats.baseData.baseCommands;
    }
}