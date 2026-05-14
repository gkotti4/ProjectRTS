using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnitController))]
public class CommandController : MonoBehaviour
{
    private UnitController unit;
    private EntityStats stats;
    private ICommand[] componentCommands;
    private ICommand activeCommand;

    void Awake()
    {
        unit = GetComponent<UnitController>();
        stats = GetComponent<EntityStats>();
        componentCommands = GetComponents<ICommand>();
    }

    void Update()
    {
        activeCommand?.Tick();
    }

    // Called by PlayerInputHandler on right click — routes based on what was hit
    public void ExecuteContextCommand(RaycastHit hit)
    {
        // Check component context commands first
        foreach (ICommand cmd in componentCommands)
        {
            if (cmd.IsContextCommand && cmd.CanExecute(hit))
            {
                activeCommand?.Cancel();
                activeCommand = cmd;
                cmd.Execute(hit);
                return;
            }
        }

        if (hit.collider == null) { unit.MoveTo(hit.point); return; }

        // Hit enemy — attack
        if (hit.collider.CompareTag("Enemy"))
        {
            unit.SetMoveTarget(hit);
            return;
        }

        // Hit resource node and unit can gather — gather
        if (hit.collider.TryGetComponent(out ResourceNode node) && stats.gatherAmount > 0)
        {
            unit.SetMoveTarget(hit);
            return;
        }

        // Hit ground — move
        unit.MoveTo(hit.point);
    }

    // Called by PlayerInputHandler on hotkey — finds matching command and executes
    public void ExecuteHotkeyCommand(HotkeySlot slot, RaycastHit hit)
    {
        // Check component commands first
        foreach (ICommand cmd in componentCommands)
        {
            if (cmd.Hotkey == slot)
            {
                activeCommand?.Cancel();
                activeCommand = cmd;
                cmd.Execute(hit);
                return;
            }
        }

        // Check base commands by hotkey
        foreach (CommandData cmd in stats.baseData.baseCommands)
        {
            if (cmd.hotkey != slot) continue;

            switch (cmd.commandType)
            {
                case CommandType.Stop:
                    activeCommand?.Cancel();
                    activeCommand = null;
                    unit.MoveTo(unit.transform.position);
                    break;
                case CommandType.AttackMove:
                    unit.MoveTo(hit.point); // TODO — attack units in range while moving
                    break;
                case CommandType.Patrol:
                    break; // TODO
                case CommandType.Garrison:
                    break; // TODO
            }
            return;
        }
    }

    // Returns all commands for UI button generation
    public List<(Sprite icon, HotkeySlot hotkey, string name, bool showButton)> GetAllCommands()
    {
        var result = new List<(Sprite icon, HotkeySlot hotkey, string name, bool showButton)>();

        foreach (CommandData cmd in stats.baseData.baseCommands)
            result.Add((cmd.icon, cmd.hotkey, cmd.commandName, cmd.showButton));

        foreach (ICommand cmd in componentCommands)
            result.Add((cmd.Icon, cmd.Hotkey, cmd.CommandName, cmd.ShowButton));

        return result;
    }
}