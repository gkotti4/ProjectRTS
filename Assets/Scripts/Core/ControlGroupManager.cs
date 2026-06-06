using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class ControlGroupManager : MonoBehaviour
{
    public static ControlGroupManager Instance { get; private set; }

    public ControlGroup[] controlGroups = new ControlGroup[9];
    public ControlGroup[] GetControlGroups() => controlGroups;


    #region Unity Lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); 
            return;
        }
        Instance = this;
        
        for (int i = 0; i < controlGroups.Length; i++)
            controlGroups[i] = new ControlGroup();
    }
    #endregion


    #region Control Group Assignment
    /// Returns the control group at the given index, null if out of range
    public ControlGroup GetControlGroup(int group)
    {
        if (group < 0 || group >= controlGroups.Length) return null;
        return controlGroups[group];
    }

    /// Assigns current selection to a control group (ONLY MILITARY UNITS CURRENTLY)
    public void AssignControlGroup(int group) // CHECK NEW
    {
        if (group < 0 || group >= controlGroups.Length)
            return;

        ControlGroup cg = controlGroups[group];

        List<MilitaryController> military = SelectionManager.Instance
            .GetSelectedObjects()
            .Select(s => s.GetGameObject().GetComponent<MilitaryController>())
            .Where(mc => mc != null)
            .ToList();

        if (military.Count == 0)
            return;

        // Clear old group contents
        foreach (ISelectable member in cg.members)
        {
            if (member.GetGameObject().TryGetComponent(out EntityController ec))
                ec.controlGroup = -1;

            if (member.GetGameObject().TryGetComponent(out MilitaryController mc))
                mc.offsetIndex = -1;
        }

        cg.members.Clear();
        cg.unitToOffsetIndex.Clear();

        // Remove selected units from whatever group they were in
        foreach (MilitaryController mc in military)
        {
            EntityController ec = mc.GetComponent<EntityController>();

            if (ec.controlGroup >= 0)
                controlGroups[ec.controlGroup].members.Remove(mc);

            ec.controlGroup = group;

            cg.members.Add(mc);
        }

        military = GetMilitaryFromGroup(cg);

        for (int i = 0; i < military.Count; i++)
        {
            military[i].offsetIndex = i;
            cg.unitToOffsetIndex[military[i].GetInstanceID()] = i;
        }

        RebuildGroupFormation(cg);
    }

    /// Selects all members of a control group
    public void SelectControlGroup(int group)
    {
        if (group < 0 || group >= controlGroups.Length) return;
        if (controlGroups[group].members.Count == 0) return;

        SelectionManager.Instance.DeselectAll();
        foreach (ISelectable s in controlGroups[group].members)
            SelectionManager.Instance.SelectExternal(s);
    }

    /// Removes a selectable from its control group - called on death/unregister (new - check)
    public void RemoveFromGroup(ISelectable selectable)
    {
        if (!selectable.GetGameObject().TryGetComponent(out EntityController ec)) return;
        if (ec.controlGroup < 0 || ec.controlGroup >= controlGroups.Length) return;
        
        controlGroups[ec.controlGroup].members.Remove(selectable);
        
        // Reset offset indices for remaining members formation
        List<MilitaryController> military = GetMilitaryFromGroup(controlGroups[ec.controlGroup]);
        for (int i = 0; i < military.Count; i++)
        {
            military[i].offsetIndex = i;
            controlGroups[ec.controlGroup].unitToOffsetIndex[military[i].gameObject.GetInstanceID()] = i;
        }
        
        // Recalculate offsets with new count
        ControlGroup cg = controlGroups[ec.controlGroup];
        if (military.Count > 0)
        {
            float width = cg.formationWidth > 0
                ? cg.formationWidth
                : military.Count * FormationManager.Instance.DefaultSpacing;
            cg.formationOffsets = FormationManager.Instance.CalculateOffsets(
                military.Count, width, cg.formation);
                
        }
        else
        {
            cg.formationOffsets.Clear();
        }
        
        ec.controlGroup = -1;
    }
    #endregion


    #region Formation
    /// Rebuilds formation offsets and anchor for a group — called on assign and on unit death
    private void RebuildGroupFormation(ControlGroup cg) // CHECK NEW
    {
        List<MilitaryController> military = GetMilitaryFromGroup(cg);

        float width = cg.formationWidth > 0
            ? cg.formationWidth
            : military.Count * FormationManager.Instance.DefaultSpacing;

        cg.formationOffsets =
            FormationManager.Instance.CalculateOffsets(
                military.Count,
                width,
                cg.formation);

        if (military.Count > 0)
        {
            Vector3 center = GetAveragePosition(military);
            float speed = military.Min(u => u.Stats.moveSpeed);

            cg.anchor = new FormationAnchor(
                center,
                military[0].transform.forward,
                speed);
        }
    }

    /// Toggles formation mode on a control group - only acts on first military unit call
    public void ToggleFormationMode(int group)
    {
        ControlGroup cg = controlGroups[group];
        if (cg == null) return;
        cg.formationMode = !cg.formationMode;
        Debug.Log("Group " + group + " formation mode: " + cg.formationMode);
    }

    /// Sets formation type on a control group and reforms in place
    public void SetFormation(int group, UnitFormation formation)
    {
        ControlGroup cg = controlGroups[group];
        if (cg == null) return;
        
        cg.formation = formation;
        
        float width = cg.formationWidth > 0
            ? cg.formationWidth
            : cg.members.Count * FormationManager.Instance.DefaultSpacing;
        cg.formationOffsets = FormationManager.Instance.CalculateOffsets(
            cg.members.Count, width, formation);

        FormationManager.Instance.ReformInPlace(cg);
    }

    /// Sets formation stance on a control group
    public void SetFormationStance(int group, UnitStance stance) // TO USE 
    {
        ControlGroup cg = controlGroups[group];
        if (cg == null) return;
        cg.formationStance = stance;
    }
    #endregion


    #region Helpers
    /// Returns military units from a control group sorted by instance ID
    public List<MilitaryController> GetMilitaryFromGroup(ControlGroup cg)
    {
        return cg.members
            .Select(s => s.GetGameObject().GetComponent<MilitaryController>())
            .Where(mc => mc != null)
            .OrderBy(mc => mc.gameObject.GetInstanceID())
            .ToList();
    }

    Vector3 GetAveragePosition(List<MilitaryController> units)
    {
        Vector3 avg = Vector3.zero;
        foreach(MilitaryController mc in units) avg += mc.transform.position;
        return avg / units.Count;
    }
    #endregion
}