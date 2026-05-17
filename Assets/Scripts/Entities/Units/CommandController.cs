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

    public void ExecuteCommand(CommandType cmdType) // Non-RightClick commands (Hotkeys/Buttons)
    {
        if (unit.TryGetComponent(out MilitaryController mil)) // Military Units
        {
            switch (cmdType)
            {
                case CommandType.Stop: mil.OrderStop(); break;
                case CommandType.AttackMove: break; // TODO            

                // Stances
                case CommandType.Aggressive: mil.OrderSetStance(UnitStance.Aggressive); break;
                case CommandType.Defensive: mil.OrderSetStance(UnitStance.Defensive); break;
                case CommandType.StandGround: mil.OrderSetStance(UnitStance.StandGround); break;
                case CommandType.NoAttack: mil.OrderSetStance(UnitStance.NoAttack); break;
            }
        }
        else if (unit.TryGetComponent(out VillagerController vil)) // Villager Units
        {
            switch (cmdType)
            {
                case CommandType.Stop: vil.OrderStop(); break;
                case CommandType.Build: vil.OrderStop(); break; // TODO 
            }
        }
    }

    // Returns command list for UI button generation
    public List<CommandData> GetAllCommands()
    {
        var commands = new List<CommandData>();
        var usedSlots = new HashSet<HotkeySlot>();
        foreach (CommandData cmd in stats.baseData.baseCommands)
        {
            if (cmd.hotkey != HotkeySlot.None && usedSlots.Contains(cmd.hotkey))
            {
                Debug.LogWarning("Duplicate hotkey " + cmd.hotkey + " on " + gameObject.name + " — skipping " + cmd.commandName);
                continue;
            }
            usedSlots.Add(cmd.hotkey);
            commands.Add(cmd);
        }
        commands.Sort((a, b) => a.hotkey.CompareTo(b.hotkey));
        return commands;
    }
}