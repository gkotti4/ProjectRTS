// SESSION: Squad Control Refactor

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(UnitController))]
public class CommandController : MonoBehaviour
{
    private UnitController unit;
    private EntityStats stats;

    #region Unity Lifecycle

    void Awake()
    {
        unit = GetComponent<UnitController>();
        stats = GetComponent<EntityStats>();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Called by PlayerInputHandler on hotkey.
    /// This is for individual unit commands only.
    /// Squad commands are routed through PlayerInputHandler.ExecuteSquadCommand().
    /// </summary>
    public void ExecuteHotkeyCommand(HotkeySlot slot, RaycastHit hit)
    {
        if (stats == null || stats.baseData == null)
            return;

        foreach (CommandData command in stats.baseData.baseCommands)
        {
            if (command.hotkey != slot)
                continue;

            ExecuteCommand(command.commandType, hit);
            return;
        }
    }

    /// <summary>
    /// Called by ActionButtonUI or hotkeys.
    /// This does NOT execute squad commands.
    /// </summary>
    public void ExecuteCommand(CommandType commandType)
    {
        ExecuteCommand(commandType, default);
    }

    public void ExecuteCommand(CommandType commandType, RaycastHit hit)
    {
        if (unit == null)
            return;

        if (unit.TryGetComponent(out VillagerController villager))
        {
            ExecuteVillagerCommand(villager, commandType, hit);
            return;
        }

        if (unit.TryGetComponent(out MilitaryController military))
        {
            ExecuteLegacyMilitaryCommand(military, commandType, hit);
            return;
        }
    }

    /// <summary>
    /// Returns command list for UI button generation.
    /// This only exposes commands stored on this unit's base data.
    /// </summary>
    public List<CommandData> GetAllCommands()
    {
        List<CommandData> commands = new List<CommandData>();
        HashSet<HotkeySlot> usedSlots = new HashSet<HotkeySlot>();

        if (stats == null || stats.baseData == null)
            return commands;

        foreach (CommandData command in stats.baseData.baseCommands)
        {
            if (command.hotkey != HotkeySlot.None && usedSlots.Contains(command.hotkey))
            {
                Debug.LogWarning(
                    "Duplicate hotkey " + command.hotkey +
                    " on " + gameObject.name +
                    " — skipping " + command.commandName);

                continue;
            }

            usedSlots.Add(command.hotkey);
            commands.Add(command);
        }

        commands.Sort((a, b) => a.hotkey.CompareTo(b.hotkey));
        return commands;
    }

    #endregion

    #region Unit Command Routing

    void ExecuteVillagerCommand(
        VillagerController villager,
        CommandType commandType,
        RaycastHit hit)
    {
        switch (commandType)
        {
            case CommandType.Stop:
                villager.OrderStop();
                return;

            case CommandType.Build:
                // Build submenu / building placement is handled by PlayerInputHandler/UIManager.
                // Do not start placement from here yet.
                villager.OrderStop();
                return;

            case CommandType.AttackMove:
            case CommandType.Patrol:
            case CommandType.Garrison:
            case CommandType.Ability:
                // TODO later.
                return;
        }
    }

    void ExecuteLegacyMilitaryCommand(
        MilitaryController military,
        CommandType commandType,
        RaycastHit hit)
    {
        // Squad-controlled military members should never receive direct commands.
        if (military.IsSquadControlled)
            return;

        switch (commandType)
        {
            case CommandType.Stop:
                military.OrderStop();
                return;

            case CommandType.AttackMove:
                if (hit.collider != null)
                    military.OrderAttackMove(hit.point);
                return;

            case CommandType.Aggressive:
                military.OrderSetStance(CombatStance.Aggressive);
                return;

            case CommandType.Defensive:
                military.OrderSetStance(CombatStance.Defensive);
                return;

            case CommandType.StandGround:
                military.OrderSetStance(CombatStance.StandGround);
                return;

            case CommandType.NoAttack:
                military.OrderSetStance(CombatStance.NoAttack);
                return;

            case CommandType.Patrol:
            case CommandType.Garrison:
            case CommandType.Ability:
                // TODO later.
                return;

            // Formation commands are squad-only now.
            case CommandType.FormationLine:
            case CommandType.FormationSpread:
            case CommandType.FormationBox:
            case CommandType.FormationCircle:
            case CommandType.FormationWedge:
                return;
        }
    }

    #endregion
}






// // [RequireComponent(typeof(UnitController))]
// // public class CommandController : MonoBehaviour
// // {
// //     private UnitController unit;
// //     private EntityStats stats;
// //
// //     void Awake()
// //     {
// //         unit = GetComponent<UnitController>();
// //         stats = GetComponent<EntityStats>();
// //     }
// //
// //     // Called by PlayerInputHandler on hotkey — explicit commands only
// //     public void ExecuteHotkeyCommand(HotkeySlot slot, RaycastHit hit)
// //     {
// //         foreach (CommandData cmd in stats.baseData.baseCommands)
// //         {
// //             if (cmd.hotkey != slot) continue;
// //
// //             ExecuteCommand(cmd.commandType);
// //             
// //             return;
// //         }
// //     }
// //
// //     public void ExecuteCommand(CommandType cmdType) // Non-RightClick commands (Hotkeys/Buttons)
// //     {
// //         if (unit.TryGetComponent(out MilitaryController mil)) // Military Commands
// //         {
// //             switch (cmdType)
// //             {
// //                 case CommandType.Stop: mil.OrderStop(); break;
// //                 case CommandType.AttackMove: break; // TODO            
// //
// //                 // Stances (set stance)
// //                 case CommandType.Aggressive: mil.OrderSetStance(UnitStance.Aggressive); break;
// //                 case CommandType.Defensive: mil.OrderSetStance(UnitStance.Defensive); break;
// //                 case CommandType.StandGround: mil.OrderSetStance(UnitStance.StandGround); break;
// //                 case CommandType.NoAttack: mil.OrderSetStance(UnitStance.NoAttack); break;
// //                 
// //                 // Formations (set formation)
// //                 case CommandType.ToggleFormationMode:
// //                     ToggleGroupFormationMode(); break;
// //                 
// //                 case CommandType.FormationLine:
// //                     SetGroupFormation(UnitFormation.Line); break;
// //                 case CommandType.FormationSpread:
// //                     SetGroupFormation(UnitFormation.Spread); break;
// //                 case CommandType.FormationBox:
// //                     SetGroupFormation(UnitFormation.Box); break;
// //                 case CommandType.FormationCircle:
// //                     SetGroupFormation(UnitFormation.Circle); break;
// //                 case CommandType.FormationWedge:
// //                     SetGroupFormation(UnitFormation.Wedge); break;
// //             }
// //         }
// //         else if (unit.TryGetComponent(out VillagerController vil)) // Villager Commands
// //         {
// //             switch (cmdType)
// //             {
// //                 case CommandType.Stop: vil.OrderStop(); break;
// //                 case CommandType.Build: vil.OrderStop(); break; // TODO 
// //             }
// //         }
// //     }
// //
// //     // Returns command list for UI button generation
// //     public List<CommandData> GetAllCommands()
// //     {
// //         var commands = new List<CommandData>();
// //         var usedSlots = new HashSet<HotkeySlot>();
// //         foreach (CommandData cmd in stats.baseData.baseCommands)
// //         {
// //             if (cmd.hotkey != HotkeySlot.None && usedSlots.Contains(cmd.hotkey))
// //             {
// //                 Debug.LogWarning("Duplicate hotkey " + cmd.hotkey + " on " + gameObject.name + " — skipping " + cmd.commandName);
// //                 continue;
// //             }
// //             usedSlots.Add(cmd.hotkey);
// //             commands.Add(cmd);
// //         }
// //         commands.Sort((a, b) => a.hotkey.CompareTo(b.hotkey));
// //         return commands;
// //     }
// //
// //
// //
// //     #region Formations
// //
// //     void ToggleGroupFormationMode()
// //     {
// //         if (unit.controlGroup < 0) return;
// //         ControlGroup cg = SelectionManager.Instance.GetControlGroup(unit.controlGroup); // LOOK HERE
// //         if (cg == null) return;
// //         cg.formationMode = true; // MUST REDO LATER - called by all units per command call
// //         Debug.Log("Formation mode: " + cg.formationMode);
// //     }
// //     
// //     
// //     /// Sets formation on the unit's control group or just this unit if ungrouped
// //     void SetGroupFormation(UnitFormation formation)
// //     {
// //         int group = unit.GetComponent<EntityController>().controlGroup;
// //     
// //         if (group >= 0) // not -1
// //         {
// //             // Control group — save and reform
// //             ControlGroup cg = SelectionManager.Instance.GetControlGroup(group);
// //             cg.formation = formation;
// //             float width = cg.formationWidth > 0 ? cg.formationWidth : 
// //                 cg.members.Count * 2f;
// //             cg.formationOffsets = FormationManager.Instance.CalculateOffsets(
// //                 cg.members.Count, width, formation); // Was using a hardcoded width of 12
// //             ReformInPlace(cg.members, formation, cg.formationOffsets);
// //         }
// //         else
// //         {
// //             // No control group — reform in place, no save
// //             List<ISelectable> selected = SelectionManager.Instance.GetSelectedObjects();
// //             float width = selected.Count * 2f;
// //             List<Vector2> offsets = FormationManager.Instance.CalculateOffsets(
// //                 selected.Count, width, formation);
// //             ReformInPlace(selected, formation, offsets);
// //         }
// //     }
// //
// //     void ReformInPlace(List<ISelectable> members, UnitFormation formation, List<Vector2> offsets)
// //     {
// //         List<UnitController> military = members
// //             .Where(s => s.GetGameObject().TryGetComponent(out UnitController u) && 
// //                         u is not VillagerController)
// //             .Select(s => s.GetGameObject().GetComponent<UnitController>())
// //             .ToList();
// //
// //         if (military.Count == 0) return;
// //
// //         // Center and facing from current unit positions
// //         Vector3 center = Vector3.zero;
// //         foreach (UnitController u in military) center += u.transform.position;
// //         center /= military.Count;
// //
// //         Vector3 facing = military[0].transform.forward;
// //
// //         List<Vector3> worldPositions = FormationManager.Instance.OffsetsToWorldPositions(
// //             offsets, center, facing);
// //
// //         var slots = FormationManager.Instance.AssignNearestSlots(military, worldPositions);
// //         foreach (var slot in slots)
// //             slot.Key.OrderMove(slot.Value);
// //
// //         FormationVisualizer.Instance.ShowSlots(new List<Vector3>(slots.Values));
// //     }
// //     #endregion
// //     
// // }
//
// using System.Collections.Generic;
// using UnityEngine;
//
// [RequireComponent(typeof(UnitController))]
// public class CommandController : MonoBehaviour
// {
//     private UnitController unit;
//     private EntityStats stats;
//
//     void Awake()
//     {
//         unit = GetComponent<UnitController>();
//         stats = GetComponent<EntityStats>();
//     }
//
//     /// Called by PlayerInputHandler on hotkey — per unit commands only
//     public void ExecuteHotkeyCommand(HotkeySlot slot, RaycastHit hit)
//     {
//         foreach (CommandData cmd in stats.baseData.baseCommands)
//         {
//             if (cmd.hotkey != slot) continue;
//             ExecuteCommand(cmd.commandType);
//             return;
//         }
//     }
//
//     /// Called by ActionButtonUI on click — per unit commands only
//     /// Group scoped commands (formations, toggle) are routed through PlayerInputHandler.ExecuteGroupCommand
//     public void ExecuteCommand(CommandType cmdType)
//     {
//         if (unit.TryGetComponent(out MilitaryController mil))
//         {
//             switch (cmdType)
//             {
//                 case CommandType.Stop:        mil.OrderStop(); break;
//                 case CommandType.AttackMove:  break; // TODO
//
//                 // Stances — per unit
//                 case CommandType.Aggressive:   mil.OrderSetStance(UnitStance.Aggressive); break;
//                 case CommandType.Defensive:    mil.OrderSetStance(UnitStance.Defensive); break;
//                 case CommandType.StandGround:  mil.OrderSetStance(UnitStance.StandGround); break;
//                 case CommandType.NoAttack:     mil.OrderSetStance(UnitStance.NoAttack); break;
//             }
//         }
//         else if (unit.TryGetComponent(out VillagerController vil))
//         {
//             switch (cmdType)
//             {
//                 case CommandType.Stop:  vil.OrderStop(); break;
//                 case CommandType.Build: vil.OrderStop(); break; // TODO
//             }
//         }
//     }
//
//     /// Returns command list for UI button generation
//     public List<CommandData> GetAllCommands()
//     {
//         var commands = new List<CommandData>();
//         var usedSlots = new HashSet<HotkeySlot>();
//
//         foreach (CommandData cmd in stats.baseData.baseCommands)
//         {
//             if (cmd.hotkey != HotkeySlot.None && usedSlots.Contains(cmd.hotkey))
//             {
//                 Debug.LogWarning("Duplicate hotkey " + cmd.hotkey + " on " + 
//                     gameObject.name + " — skipping " + cmd.commandName);
//                 continue;
//             }
//             usedSlots.Add(cmd.hotkey);
//             commands.Add(cmd);
//         }
//
//         commands.Sort((a, b) => a.hotkey.CompareTo(b.hotkey));
//         return commands;
//     }
// }