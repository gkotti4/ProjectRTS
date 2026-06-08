// SESSION: Squad Control Refactor

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SquadController : MonoBehaviour, ISelectable
{
    #region Fields

    [Header("Identity")]
    [SerializeField] private SquadCategory squadCategory = SquadCategory.Infantry;
    [SerializeField] private int maxMembers = 20;

    [Header("Formation")]
    [SerializeField] private SquadFormation formation = SquadFormation.Line;
    [SerializeField] private float formationWidth = -1f;
    [SerializeField] private float defaultSpacing = 2f;
    [SerializeField] private float slotUpdateThreshold = 0.25f;

    [Header("Movement")]
    [SerializeField] private SquadMoveMode moveMode = SquadMoveMode.IdleFormed;
    [SerializeField] private float turnSpeed = 540f;

    [Header("Slot Validity")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float slotCheckRadius = 0.45f;
    [SerializeField] private float navMeshSampleRadius = 0.75f;
    [SerializeField] private float slotValidationInterval = 0.15f;
    [SerializeField] private int badSlotCountToBreak = 2;
    [SerializeField] private float badSlotRatioToBreak = 0.25f;

    [Header("Loose / Reform")]
    [SerializeField] private float reformCheckInterval = 0.25f;
    [SerializeField] private float reformMemberDistance = 1.25f;
    [SerializeField] private float reformRatioRequired = 0.75f;

    [Header("Behavior")]
    [SerializeField] private CombatStance stance = CombatStance.Aggressive;

    [Header("Members")]
    [SerializeField] private List<SquadMemberController> members = new List<SquadMemberController>();

    private NavMeshAgent squadAgent;
    private List<Vector2> formationOffsets = new List<Vector2>();
    private List<Vector3> finalSlots = new List<Vector3>();

    private Vector3 facing = Vector3.forward;
    private Vector3 desiredFacing = Vector3.forward;
    private Vector3 finalDestination;

    private float slotValidationTimer = 0f;
    private float reformCheckTimer = 0f;

    private bool isSelected = false;
    private bool isInitialized = false;

    #endregion

    #region Properties

    public SquadCategory Category => squadCategory;
    public SquadFormation Formation => formation;
    public CombatStance Stance => stance;
    public SquadMoveMode MoveMode => moveMode;

    public IReadOnlyList<SquadMemberController> Members => members;
    public int MemberCount => members.Count;
    public int MaxMembers => maxMembers;
    public bool HasRoom => members.Count < maxMembers;

    public bool IsSelected => isSelected;
    public bool IsDragSelectable => true;

    public FactionInstance Faction
    {
        get
        {
            foreach (SquadMemberController member in members)
            {
                if (member != null && member.Stats != null)
                    return member.Stats.faction;
            }

            return null;
        }
    }

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        squadAgent = GetComponent<NavMeshAgent>();

        squadAgent.updateRotation = false;
        squadAgent.angularSpeed = 99999f;
        squadAgent.acceleration = 99999f;
        squadAgent.autoBraking = false;
    }

    void Start()
    {
        SelectionManager.Instance.RegisterSelectable(this);
        SquadManager.Instance?.RegisterSquad(this);

        if (!isInitialized)
            InitializeEmptySquad();
    }

    void Update()
    {
        switch (moveMode)
        {
            case SquadMoveMode.IdleFormed:
                UpdateIdleFormed();
                break;

            case SquadMoveMode.FormedMove:
                UpdateFormedMove();
                break;

            case SquadMoveMode.LooseMove:
                UpdateLooseMove();
                break;

            case SquadMoveMode.Reforming:
                UpdateReforming();
                break;
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.UnregisterSelectable(this);

        if (SquadManager.Instance != null)
            SquadManager.Instance.UnregisterSquad(this);
    }

    #endregion

    #region Initialization

    void InitializeEmptySquad()
    {
        facing = NormalizeFacing(transform.forward);
        desiredFacing = facing;
        finalDestination = transform.position;
        finalSlots = GetWorldSlots(transform.position, facing);
        moveMode = SquadMoveMode.IdleFormed;
        isInitialized = true;
    }

    public void InitializeSquad(
        List<SquadMemberController> startingMembers,
        SquadFormation startingFormation = SquadFormation.Line,
        CombatStance startingStance = CombatStance.Aggressive)
    {
        if (startingMembers == null)
        {
            Debug.LogError("InitializeSquad failed: startingMembers is null.");
            return;
        }

        if (!MembersHaveSameFaction(startingMembers))
        {
            Debug.LogError("InitializeSquad failed: squad members have mixed factions.");
            return;
        }

        members.Clear();

        formation = startingFormation;
        stance = startingStance;

        facing = NormalizeFacing(transform.forward);
        desiredFacing = facing;
        finalDestination = transform.position;

        foreach (SquadMemberController member in startingMembers)
        {
            if (member == null) continue;

            member.transform.SetParent(null, true);

            members.Add(member);
            member.JoinSquad(this, members.Count - 1);
        }

        RebuildFormation();

        finalSlots = GetWorldSlots(transform.position, facing);
        PlaceMembersInInitialSlots();

        moveMode = SquadMoveMode.IdleFormed;
        isInitialized = true;
    }

    void PlaceMembersInInitialSlots()
    {
        List<Vector3> slots = GetWorldSlots(transform.position, facing);

        for (int i = 0; i < members.Count && i < slots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 validSlot = GetNearestValidPointForSlot(slots[i]);

            if (member.Agent != null && member.Agent.enabled)
                member.Agent.Warp(validSlot);
            else
                member.transform.position = validSlot;

            member.SetLastSlotPosition(validSlot);
        }
    }

    #endregion

    #region Selection

    public void OnSelect()
    {
        isSelected = true;

        foreach (SquadMemberController member in members)
        {
            if (member == null || member.Unit == null) continue;
            member.ShowSelectionVisual();
        }
    }

    public void OnDeselect()
    {
        isSelected = false;

        foreach (SquadMemberController member in members)
        {
            if (member == null || member.Unit == null) continue;
            member.HideSelectionVisual();
        }
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    #endregion

    #region Orders

    public void OrderMove(Vector3 destination)
    {
        Vector3 resolvedFacing = ResolveFacing(destination);
        OrderMove(destination, resolvedFacing);
    }

    public void OrderMove(
        Vector3 destination,
        Vector3 orderedFacing,
        float requestedFormationWidth = -1f)
    {
        if (members.Count == 0) return;

        if (requestedFormationWidth > 0f)
            formationWidth = requestedFormationWidth;

        finalDestination = destination;
        desiredFacing = NormalizeFacing(orderedFacing);

        RebuildFormation();
        finalSlots = GetWorldSlots(finalDestination, desiredFacing);

        squadAgent.isStopped = false;
        squadAgent.stoppingDistance = 0.2f;
        squadAgent.SetDestination(finalDestination);

        moveMode = SquadMoveMode.FormedMove;
        slotValidationTimer = 0f;

        FormationVisualizer.Instance?.ShowSlots(finalSlots);
    }

    public void OrderStop()
    {
        if (squadAgent != null && squadAgent.enabled)
            squadAgent.ResetPath();

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;
            member.Stop();
        }

        moveMode = SquadMoveMode.IdleFormed;
    }

    public void OrderAttack(EntityController target)
    {
        if (target == null) return;

        // First pass:
        // Move toward target. Squad combat brain comes later.
        OrderMove(target.transform.position);
    }

    public void SetFormation(SquadFormation newFormation)
    {
        formation = newFormation;
        RebuildFormation();

        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);
        FormationVisualizer.Instance?.ShowSlots(currentSlots);
    }

    public void SetStance(CombatStance newStance)
    {
        stance = newStance;
    }

    public List<Vector3> GetPreviewSlots(
        Vector3 center,
        Vector3 slotFacing,
        float requestedFormationWidth = -1f)
    {
        float oldWidth = formationWidth;

        if (requestedFormationWidth > 0f)
            formationWidth = requestedFormationWidth;

        RebuildFormation();
        List<Vector3> previewSlots = GetWorldSlots(center, NormalizeFacing(slotFacing));

        formationWidth = oldWidth;
        RebuildFormation();

        return previewSlots;
    }

    #endregion

    #region Movement Modes

    void UpdateIdleFormed()
    {
        SmoothFacingToward(desiredFacing);
        UpdateMembersToCurrentSlots();
    }

    void UpdateFormedMove()
    {
        UpdateFacingFromAgent();
        UpdateMembersToCurrentSlots();

        slotValidationTimer -= Time.deltaTime;

        if (slotValidationTimer <= 0f)
        {
            slotValidationTimer = slotValidationInterval;

            int badSlots = CountBadCurrentSlots();

            if (ShouldBreakFormation(badSlots))
            {
                SwitchToLooseMove();
                return;
            }
        }

        if (!squadAgent.pathPending &&
            squadAgent.remainingDistance <= squadAgent.stoppingDistance)
        {
            moveMode = SquadMoveMode.Reforming;
            reformCheckTimer = 0f;
        }
    }

    void UpdateLooseMove()
    {
        UpdateSquadCenterFromMembers();

        reformCheckTimer -= Time.deltaTime;

        if (reformCheckTimer <= 0f)
        {
            reformCheckTimer = reformCheckInterval;

            if (EnoughMembersNearFinalSlots())
                BeginReforming();
        }
    }

    void UpdateReforming()
    {
        SmoothFacingToward(desiredFacing);
        UpdateMembersToCurrentSlots();

        reformCheckTimer -= Time.deltaTime;

        if (reformCheckTimer <= 0f)
        {
            reformCheckTimer = reformCheckInterval;

            if (EnoughMembersNearCurrentSlots())
                moveMode = SquadMoveMode.IdleFormed;
        }
    }

    #endregion

    #region Formed Movement

    void UpdateMembersToCurrentSlots()
    {
        if (members.Count == 0) return;

        if (formationOffsets.Count != members.Count)
            RebuildFormation();

        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);

        for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 slot = GetNearestValidPointForSlot(currentSlots[i]);
            member.MoveToSlot(slot, slotUpdateThreshold);
        }
    }

    int CountBadCurrentSlots()
    {
        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);
        int badCount = 0;

        for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            if (IsCurrentSlotBad(member, currentSlots[i]))
                badCount++;
        }

        return badCount;
    }

    bool IsCurrentSlotBad(SquadMemberController member, Vector3 slot)
    {
        if (!IsSlotNearNavMesh(slot))
            return true;

        if (IsSlotBlockedByObstacle(slot))
            return true;

        if (!CanMemberPathToCurrentSlot(member, slot))
            return true;

        return false;
    }

    bool ShouldBreakFormation(int badSlotCount)
    {
        if (members.Count == 0) return false;

        float badRatio = (float)badSlotCount / members.Count;

        return badSlotCount >= badSlotCountToBreak ||
               badRatio >= badSlotRatioToBreak;
    }

    #endregion

    #region Loose Movement / Reform

    void SwitchToLooseMove()
    {
        if (squadAgent != null && squadAgent.enabled)
            squadAgent.ResetPath();

        moveMode = SquadMoveMode.LooseMove;
        finalSlots = GetWorldSlots(finalDestination, desiredFacing);

        for (int i = 0; i < members.Count && i < finalSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 target = GetNearestValidPointForSlot(finalSlots[i]);
            member.MoveToPoint(target);
        }

        FormationVisualizer.Instance?.ShowSlots(finalSlots);
    }

    void BeginReforming()
    {
        UpdateSquadCenterFromMembers();

        facing = desiredFacing;
        finalDestination = transform.position;
        finalSlots = GetWorldSlots(transform.position, facing);

        moveMode = SquadMoveMode.Reforming;
        reformCheckTimer = 0f;
    }

    bool EnoughMembersNearFinalSlots()
    {
        if (members.Count == 0 || finalSlots.Count == 0)
            return false;

        int nearCount = 0;

        for (int i = 0; i < members.Count && i < finalSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            Vector3 slot = GetNearestValidPointForSlot(finalSlots[i]);

            if (member.IsNear(slot, reformMemberDistance))
                nearCount++;
        }

        float ratio = (float)nearCount / members.Count;
        return ratio >= reformRatioRequired;
    }

    bool EnoughMembersNearCurrentSlots()
    {
        List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);

        if (members.Count == 0 || currentSlots.Count == 0)
            return false;

        int nearCount = 0;

        for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
        {
            SquadMemberController member = members[i];
            if (member == null) continue;

            if (member.IsNear(currentSlots[i], reformMemberDistance))
                nearCount++;
        }

        float ratio = (float)nearCount / members.Count;
        return ratio >= reformRatioRequired;
    }

    void UpdateSquadCenterFromMembers()
    {
        if (members.Count == 0) return;

        Vector3 center = GetAverageMemberPosition();

        if (squadAgent != null && squadAgent.enabled)
            squadAgent.Warp(center);
        else
            transform.position = center;
    }

    #endregion

    #region Slot Validity

    bool IsSlotNearNavMesh(Vector3 slot)
    {
        return NavMesh.SamplePosition(
            slot,
            out _,
            navMeshSampleRadius,
            NavMesh.AllAreas);
    }

    bool IsSlotBlockedByObstacle(Vector3 slot)
    {
        return Physics.CheckSphere(
            slot + Vector3.up * 0.3f,
            slotCheckRadius,
            obstacleLayers,
            QueryTriggerInteraction.Ignore);
    }

    bool CanMemberPathToCurrentSlot(SquadMemberController member, Vector3 slot)
    {
        if (member == null || member.Agent == null || !member.Agent.enabled)
            return false;

        Vector3 validSlot = GetNearestValidPointForSlot(slot);

        NavMeshPath path = new NavMeshPath();

        bool hasPath = NavMesh.CalculatePath(
            member.transform.position,
            validSlot,
            NavMesh.AllAreas,
            path);

        return hasPath && path.status == NavMeshPathStatus.PathComplete;
    }

    Vector3 GetNearestValidPointForSlot(Vector3 slot)
    {
        if (NavMesh.SamplePosition(
                slot,
                out NavMeshHit hit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

        return slot;
    }

    #endregion

    #region Formation

    void RebuildFormation()
    {
        CleanNullMembers();

        float width = formationWidth > 0f
            ? formationWidth
            : GetDefaultFormationWidth();

        if (FormationCalculator.Instance != null)
        {
            formationOffsets = FormationCalculator.Instance.CalculateOffsets(
                members.Count,
                width,
                formation);
        }
        else
        {
            formationOffsets = BuildFallbackLineOffsets(members.Count);
        }

        ReassignSlotIndices();
    }

    List<Vector3> GetWorldSlots(Vector3 center, Vector3 slotFacing)
    {
        slotFacing = NormalizeFacing(slotFacing);

        if (FormationCalculator.Instance != null)
        {
            return FormationCalculator.Instance.ConvertOffsetsToWorldPositions(
                formationOffsets,
                center,
                slotFacing);
        }

        List<Vector3> result = new List<Vector3>();
        Vector3 right = new Vector3(slotFacing.z, 0f, -slotFacing.x).normalized;

        foreach (Vector2 offset in formationOffsets)
            result.Add(center + right * offset.x + slotFacing * offset.y);

        return result;
    }

    float GetDefaultFormationWidth()
    {
        if (FormationCalculator.Instance != null)
            return Mathf.Max(1, members.Count) * FormationCalculator.Instance.DefaultSpacing;

        return Mathf.Max(1, members.Count) * defaultSpacing;
    }

    List<Vector2> BuildFallbackLineOffsets(int count)
    {
        List<Vector2> offsets = new List<Vector2>();
        if (count <= 0) return offsets;

        float rowWidth = (count - 1) * defaultSpacing;

        for (int i = 0; i < count; i++)
        {
            float x = i * defaultSpacing - rowWidth / 2f;
            offsets.Add(new Vector2(x, 0f));
        }

        return offsets;
    }

    #endregion

    #region Members

    public bool CanAcceptMember(SquadMemberController member)
    {
        if (member == null) return false;
        if (!HasRoom) return false;

        if (members.Count == 0)
            return true;

        if (member.Stats == null || member.Stats.faction == null)
            return false;

        return member.Stats.faction == Faction;
    }

    public bool AddMember(SquadMemberController member)
    {
        if (!CanAcceptMember(member)) return false;

        if (member.Squad != null && member.Squad != this)
            member.Squad.RemoveMember(member);

        member.transform.SetParent(null, true);

        members.Add(member);
        member.JoinSquad(this, members.Count - 1);

        RebuildFormation();

        if (moveMode == SquadMoveMode.IdleFormed)
            moveMode = SquadMoveMode.Reforming;

        return true;
    }

    public void RemoveMember(SquadMemberController member)
    {
        if (member == null) return;
        if (!members.Contains(member)) return;

        members.Remove(member);
        member.LeaveSquad();

        ReassignSlotIndices();
        RebuildFormation();

        if (members.Count == 0)
            Destroy(gameObject);
    }

    void ReassignSlotIndices()
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == null) continue;
            members[i].SetSlotIndex(i);
        }
    }

    void CleanNullMembers()
    {
        members = members.Where(m => m != null).ToList();
    }

    bool MembersHaveSameFaction(List<SquadMemberController> startingMembers)
    {
        FactionInstance faction = null;

        foreach (SquadMemberController member in startingMembers)
        {
            if (member == null || member.Stats == null)
                continue;

            if (faction == null)
            {
                faction = member.Stats.faction;
                continue;
            }

            if (member.Stats.faction != faction)
                return false;
        }

        return true;
    }

    Vector3 GetAverageMemberPosition()
    {
        Vector3 avg = Vector3.zero;
        int count = 0;

        foreach (SquadMemberController member in members)
        {
            if (member == null) continue;

            avg += member.transform.position;
            count++;
        }

        return count > 0 ? avg / count : transform.position;
    }

    #endregion

    #region Merge

    public bool CanMergeWith(SquadController other)
    {
        if (other == null) return false;
        if (other == this) return false;
        if (other.Category != Category) return false;
        if (members.Count + other.MemberCount > maxMembers) return false;
        if (Faction != null && other.Faction != null && Faction != other.Faction) return false;

        return true;
    }

    public bool AbsorbSquad(SquadController other)
    {
        if (!CanMergeWith(other)) return false;

        List<SquadMemberController> incoming = other.members.ToList();

        foreach (SquadMemberController member in incoming)
            AddMember(member);

        other.members.Clear();
        Destroy(other.gameObject);

        RebuildFormation();
        moveMode = SquadMoveMode.Reforming;

        return true;
    }

    #endregion

    #region Facing

    Vector3 ResolveFacing(Vector3 destination)
    {
        Vector3 dir = destination - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return facing == Vector3.zero ? Vector3.forward : facing;

        return dir.normalized;
    }

    void UpdateFacingFromAgent()
    {
        if (squadAgent == null) return;

        if (squadAgent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 dir = squadAgent.velocity;
            dir.y = 0f;

            if (dir != Vector3.zero)
                desiredFacing = dir.normalized;
        }

        SmoothFacingToward(desiredFacing);
    }

    void SmoothFacingToward(Vector3 targetFacing)
    {
        targetFacing = NormalizeFacing(targetFacing);

        facing = Vector3.RotateTowards(
            facing,
            targetFacing,
            turnSpeed * Mathf.Deg2Rad * Time.deltaTime,
            0f);
    }

    Vector3 NormalizeFacing(Vector3 dir)
    {
        dir.y = 0f;

        if (dir == Vector3.zero)
            return Vector3.forward;

        return dir.normalized;
    }

    #endregion
}





// // SESSION: Squad Control
//
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
// using UnityEngine.AI;
// using UnityEngine.Rendering.Universal;
//
// [RequireComponent(typeof(NavMeshAgent))]
//
// public class SquadController : MonoBehaviour, ISelectable
// {
//     [Header("Identity")]
//     [SerializeField] private SquadCategory squadCategory = SquadCategory.Infantry;
//     [SerializeField] private int maxMembers = 20;
//
//     [Header("Formation")]
//     [SerializeField] private UnitFormation formation = UnitFormation.Line;
//     [SerializeField] private float formationWidth = -1f;
//     [SerializeField] private float defaultSpacing = 2f;
//     [SerializeField] private float slotUpdateThreshold = 0.25f;
//
//     [Header("Movement Mode")]
//     [SerializeField] private SquadMoveMode moveMode = SquadMoveMode.IdleFormed;
//     [SerializeField] private float turnSpeed = 540f;
//
//     [Header("Slot Validity")]
//     [SerializeField] private LayerMask obstacleLayers;
//     [SerializeField] private float slotCheckRadius = 0.45f;
//     [SerializeField] private float navMeshSampleRadius = 0.75f;
//     [SerializeField] private float slotValidationInterval = 0.15f;
//     [SerializeField] private int badSlotCountToBreak = 2;
//     [SerializeField] private float badSlotRatioToBreak = 0.25f;
//
//     [Header("Loose/Reform")]
//     [SerializeField] private float reformCheckInterval = 0.25f;
//     [SerializeField] private float reformMemberDistance = 1.25f;
//     [SerializeField] private float reformRatioRequired = 0.75f;
//
//     [Header("Behavior")]
//     [SerializeField] private UnitStance stance = UnitStance.Aggressive;
//     
//     [Header("Members")]
//     [SerializeField] private List<SquadMemberController> members = new List<SquadMemberController>();
//     
//     private NavMeshAgent squadAgent;
//     private List<Vector2> formationOffsets = new List<Vector2>();
//
//     private Vector3 facing = Vector3.forward;
//     private Vector3 desiredFacing = Vector3.forward;
//     private Vector3 finalDestination;
//     private List<Vector3> finalSlots = new List<Vector3>();
//
//     private float slotValidationTimer = 0f;
//     private float reformCheckTimer = 0f;
//     private bool isSelected = false;
//
//     public SquadCategory Category => squadCategory;
//     public UnitFormation Formation => formation;
//     public UnitStance Stance => stance;
//     public SquadMoveMode MoveMode => moveMode;
//
//     public IReadOnlyList<SquadMemberController> Members => members;
//     public int MemberCount => members.Count;
//     public int MaxMembers => maxMembers;
//     public bool HasRoom => members.Count < maxMembers;
//
//     public bool IsSelected => isSelected;
//     public bool IsDragSelectable => true;
//
//
//     public FactionInstance Faction
//     {
//         get
//         {
//             // REFACTOR: check if all factions are the same in all members
//             foreach (SquadMemberController member in members)
//             {
//                 if (member != null && member.Stats != null)
//                 {
//                     return member.Stats.faction;
//                 }
//             }
//             
//             return null;
//         }
//     }
//     
//     
//     void Awake()
//     {
//         squadAgent = GetComponent<NavMeshAgent>();
//         squadAgent.updateRotation = false;
//         squadAgent.angularSpeed = 99999f;
//         squadAgent.acceleration = 99999f;
//         squadAgent.autoBraking = false;
//
//         // if (selectionDecal != null)
//         //     selectionDecal.enabled = false;
//     }
//     
//
//     void Start()
//     {
//         SelectionManager.Instance.RegisterSelectable(this);
//         SquadManager.Instance?.RegisterSquad(this);
//
//         // Factory-created squads call InitializeSquad().
//         // If this squad was manually placed with no initialization, keep it safe.
//
//         // CHECK: if we need 'isInitialized' var check here
//         finalDestination = transform.position;
//         desiredFacing = facing;
//         finalSlots = GetWorldSlots(transform.position, facing);
//         moveMode = SquadMoveMode.IdleFormed;
//         
//     }
//     
//
//     void OnDestroy()
//     {
//         if (SelectionManager.Instance != null)
//             SelectionManager.Instance.UnregisterSelectable(this);
//
//         if (SquadManager.Instance != null)
//             SquadManager.Instance.UnregisterSquad(this);
//     }
//     
//     
//     void Update()
//     {
//         switch (moveMode)
//         {
//             case SquadMoveMode.IdleFormed:
//                 UpdateIdleFormed();
//                 break;
//
//             case SquadMoveMode.FormedMove:
//                 UpdateFormedMove();
//                 break;
//
//             case SquadMoveMode.LooseMove:
//                 UpdateLooseMove();
//                 break;
//
//             case SquadMoveMode.Reforming:
//                 UpdateReforming();
//                 break;
//         }
//     }
//     
//     
//     // SESSION: Squad Control
//     public void InitializeSquad(
//         List<SquadMemberController> startingMembers,
//         UnitFormation startingFormation = UnitFormation.Line,
//         UnitStance startingStance = UnitStance.Aggressive)
//     {
//         if (!MembersHaveSameFaction(startingMembers))
//         {
//             Debug.LogError("InitializeSquad failed: squad members have mixed factions.");
//             return;
//         }
//         
//         members.Clear();
//
//         formation = startingFormation;
//         stance = startingStance;
//
//         facing = transform.forward;
//         facing.y = 0f;
//
//         if (facing == Vector3.zero)
//             facing = Vector3.forward;
//
//         desiredFacing = facing;
//         finalDestination = transform.position;
//
//         foreach (SquadMemberController member in startingMembers)
//         {
//             if (member == null) continue;
//
//             // Make sure member is a world object, not physically childed to squad.
//             member.transform.SetParent(null, true);
//
//             members.Add(member);
//             member.JoinSquad(this, members.Count - 1);
//         }
//
//         RebuildFormation();
//
//         finalSlots = GetWorldSlots(transform.position, facing);
//
//         PlaceMembersInInitialSlots();
//
//         moveMode = SquadMoveMode.IdleFormed;
//         // isInitialized = true;
//     }
//     
//     void PlaceMembersInInitialSlots()
//     {
//         List<Vector3> slots = GetWorldSlots(transform.position, facing);
//
//         for (int i = 0; i < members.Count && i < slots.Count; i++)
//         {
//             SquadMemberController member = members[i];
//             if (member == null) continue;
//
//             Vector3 validSlot = GetNearestValidPointForSlot(slots[i]);
//
//             if (member.Agent != null && member.Agent.enabled)
//                 member.Agent.Warp(validSlot);
//             else
//                 member.transform.position = validSlot;
//         }
//     }
//     
//     
//     
//     
//     
//     
//     
//     
//     // SESSION: Squad Control Refactor
//     public void OrderMove(Vector3 destination, Vector3 orderedFacing, float requestedFormationWidth = -1f)
//     {
//         if (requestedFormationWidth > 0f)
//             formationWidth = requestedFormationWidth;
//
//         finalDestination = destination;
//
//         orderedFacing.y = 0f;
//         desiredFacing = orderedFacing == Vector3.zero
//             ? ResolveFacing(destination)
//             : orderedFacing.normalized;
//
//         RebuildFormation();
//         finalSlots = GetWorldSlots(finalDestination, desiredFacing);
//
//         squadAgent.isStopped = false;
//         squadAgent.stoppingDistance = 0.2f;
//         squadAgent.SetDestination(finalDestination);
//
//         moveMode = SquadMoveMode.FormedMove;
//         slotValidationTimer = 0f;
//
//         FormationVisualizer.Instance?.ShowSlots(finalSlots);
//     }
//
//     // SESSION: Squad Control Refactor
//     public List<Vector3> GetPreviewSlots(Vector3 center, Vector3 slotFacing, float requestedFormationWidth = -1f)
//     {
//         float oldWidth = formationWidth;
//
//         if (requestedFormationWidth > 0f)
//             formationWidth = requestedFormationWidth;
//
//         RebuildFormation();
//         List<Vector3> previewSlots = GetWorldSlots(center, slotFacing);
//
//         formationWidth = oldWidth;
//         RebuildFormation();
//
//         return previewSlots;
//     }
//     
//     
//     
//     
//     
//     
//     
//     
//     
//     
//     
//     
//     
//     #region Selection
//
//     public void OnSelect()
//     {
//         isSelected = true;
//         // if (selectionDecal != null)
//         //     selectionDecal.enabled = true;
//
//         foreach (SquadMemberController member in members)
//         {
//             member.Unit.OnSelect();
//         }
//         
//         Debug.Log("OnSelect: Squad " + this.GetInstanceID());
//     }
//
//     
//     public void OnDeselect()
//     {
//         isSelected = false;
//         // if (selectionDecal != null)
//         //     selectionDecal.enabled = false;
//         
//         foreach (SquadMemberController member in members)
//         {
//             member.Unit.OnDeselect();
//         }
//         
//         Debug.Log("OnDeselect: Squad " + this.GetInstanceID());
//     }
//     
//
//     public GameObject GetGameObject()
//     {
//         return gameObject;
//     }
//
//     #endregion
//
//     
//     #region Orders
//
//     public void OrderMove(Vector3 destination)
//     {
//         if (members.Count == 0) return;
//
//         finalDestination = destination;
//         desiredFacing = ResolveFacing(destination);
//
//         RebuildFormation();
//         finalSlots = GetWorldSlots(finalDestination, desiredFacing);
//
//         squadAgent.isStopped = false;
//         squadAgent.stoppingDistance = 0.2f;
//         squadAgent.SetDestination(finalDestination);
//
//         moveMode = SquadMoveMode.FormedMove;
//         slotValidationTimer = 0f;
//
//         FormationVisualizer.Instance?.ShowSlots(finalSlots);
//     }
//
//     
//     public void OrderStop()
//     {
//         if (squadAgent != null && squadAgent.enabled)
//             squadAgent.ResetPath();
//
//         foreach (SquadMemberController member in members)
//             if (member != null)
//                 member.Stop();
//
//         moveMode = SquadMoveMode.IdleFormed;
//     }
//
//     
//     public void OrderAttack(EntityController target)
//     {
//         if (target == null) return;
//
//         // First pass:
//         // squad moves toward target.
//         // actual squad combat brain comes later.
//         OrderMove(target.transform.position);
//     }
//
//     
//     public void SetFormation(UnitFormation newFormation)
//     {
//         formation = newFormation;
//         RebuildFormation();
//
//         finalSlots = GetWorldSlots(transform.position, facing);
//         FormationVisualizer.Instance?.ShowSlots(finalSlots);
//     }
//
//     
//     public void SetStance(UnitStance newStance)
//     {
//         stance = newStance;
//     }
//
//     #endregion
//
//     
//     #region Movement Modes
//
//     void UpdateIdleFormed()
//     {
//         SmoothFacingToward(desiredFacing);
//         UpdateMembersToCurrentSlots();
//     }
//
//     
//     void UpdateFormedMove()
//     {
//         UpdateFacingFromAgent();
//         UpdateMembersToCurrentSlots();
//
//         slotValidationTimer -= Time.deltaTime;
//         if (slotValidationTimer <= 0f)
//         {
//             slotValidationTimer = slotValidationInterval;
//
//             int badSlots = CountBadCurrentSlots();
//             if (ShouldBreakFormation(badSlots))
//             {
//                 SwitchToLooseMove();
//                 return;
//             }
//         }
//
//         if (!squadAgent.pathPending && squadAgent.remainingDistance <= squadAgent.stoppingDistance)
//         {
//             moveMode = SquadMoveMode.Reforming;
//             reformCheckTimer = 0f;
//         }
//     }
//
//     
//     void UpdateLooseMove()
//     {
//         UpdateSquadCenterFromMembers();
//
//         reformCheckTimer -= Time.deltaTime;
//         if (reformCheckTimer <= 0f)
//         {
//             reformCheckTimer = reformCheckInterval;
//
//             if (EnoughMembersNearFinalSlots())
//                 BeginReforming();
//         }
//     }
//     
//
//     void UpdateReforming()
//     {
//         SmoothFacingToward(desiredFacing);
//         UpdateMembersToCurrentSlots();
//
//         reformCheckTimer -= Time.deltaTime;
//         if (reformCheckTimer <= 0f)
//         {
//             reformCheckTimer = reformCheckInterval;
//
//             if (EnoughMembersNearCurrentSlots())
//                 moveMode = SquadMoveMode.IdleFormed;
//         }
//     }
//
//     #endregion
//
//     
//     #region Formed Movement
//
//     void UpdateMembersToCurrentSlots()
//     {
//         if (members.Count == 0) return;
//         if (formationOffsets.Count != members.Count)
//             RebuildFormation();
//
//         List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);
//
//         for (int i = 0; i < members.Count; i++)
//         {
//             SquadMemberController member = members[i];
//             if (member == null) continue;
//             if (i >= currentSlots.Count) continue;
//
//             Vector3 slot = GetNearestValidPointForSlot(currentSlots[i]);
//             member.MoveToSlot(slot, slotUpdateThreshold);
//         }
//     }
//
//     
//     int CountBadCurrentSlots()
//     {
//         List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);
//         int badCount = 0;
//
//         for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
//         {
//             SquadMemberController member = members[i];
//             if (member == null) continue;
//
//             if (IsCurrentSlotBad(member, currentSlots[i]))
//                 badCount++;
//         }
//
//         return badCount;
//     }
//
//     
//     bool IsCurrentSlotBad(SquadMemberController member, Vector3 slot)
//     {
//         if (!IsSlotNearNavMesh(slot))
//             return true;
//
//         if (IsSlotBlockedByObstacle(slot))
//             return true;
//
//         if (!CanMemberPathToCurrentSlot(member, slot))
//             return true;
//
//         return false;
//     }
//
//     
//     bool ShouldBreakFormation(int badSlotCount)
//     {
//         if (members.Count == 0) return false;
//
//         float ratio = (float)badSlotCount / members.Count;
//
//         return badSlotCount >= badSlotCountToBreak ||
//                ratio >= badSlotRatioToBreak;
//     }
//
//     #endregion
//
//     
//     #region Loose Movement
//
//     void SwitchToLooseMove()
//     {
//         if (squadAgent != null && squadAgent.enabled)
//             squadAgent.ResetPath();
//
//         moveMode = SquadMoveMode.LooseMove;
//         finalSlots = GetWorldSlots(finalDestination, desiredFacing);
//
//         for (int i = 0; i < members.Count && i < finalSlots.Count; i++)
//         {
//             SquadMemberController member = members[i];
//             if (member == null) continue;
//
//             Vector3 target = GetNearestValidPointForSlot(finalSlots[i]);
//             member.MoveToPoint(target);
//         }
//
//         FormationVisualizer.Instance?.ShowSlots(finalSlots);
//     }
//
//     
//     void BeginReforming()
//     {
//         UpdateSquadCenterFromMembers();
//
//         facing = desiredFacing;
//         finalDestination = transform.position;
//         finalSlots = GetWorldSlots(transform.position, facing);
//
//         moveMode = SquadMoveMode.Reforming;
//         reformCheckTimer = 0f;
//     }
//
//     
//     bool EnoughMembersNearFinalSlots()
//     {
//         if (members.Count == 0 || finalSlots.Count == 0)
//             return false;
//
//         int nearCount = 0;
//
//         for (int i = 0; i < members.Count && i < finalSlots.Count; i++)
//         {
//             SquadMemberController member = members[i];
//             if (member == null) continue;
//
//             Vector3 slot = GetNearestValidPointForSlot(finalSlots[i]);
//             if (member.IsNear(slot, reformMemberDistance))
//                 nearCount++;
//         }
//
//         float ratio = (float)nearCount / members.Count;
//         return ratio >= reformRatioRequired;
//     }
//
//     
//     bool EnoughMembersNearCurrentSlots()
//     {
//         List<Vector3> currentSlots = GetWorldSlots(transform.position, facing);
//
//         if (members.Count == 0 || currentSlots.Count == 0)
//             return false;
//
//         int nearCount = 0;
//
//         for (int i = 0; i < members.Count && i < currentSlots.Count; i++)
//         {
//             SquadMemberController member = members[i];
//             if (member == null) continue;
//
//             if (member.IsNear(currentSlots[i], reformMemberDistance))
//                 nearCount++;
//         }
//
//         float ratio = (float)nearCount / members.Count;
//         return ratio >= reformRatioRequired;
//     }
//     
//
//     void UpdateSquadCenterFromMembers()
//     {
//         if (members.Count == 0) return;
//
//         Vector3 avg = Vector3.zero;
//         int count = 0;
//
//         foreach (SquadMemberController member in members)
//         {
//             if (member == null) continue;
//             avg += member.transform.position;
//             count++;
//         }
//
//         if (count == 0) return;
//
//         transform.position = avg / count;
//     }
//
//     #endregion
//
//     
//     #region Slot Validity
//
//     bool IsSlotNearNavMesh(Vector3 slot)
//     {
//         return NavMesh.SamplePosition(
//             slot,
//             out _,
//             navMeshSampleRadius,
//             NavMesh.AllAreas);
//     }
//
//     
//     bool IsSlotBlockedByObstacle(Vector3 slot)
//     {
//         return Physics.CheckSphere(
//             slot + Vector3.up * 0.3f,
//             slotCheckRadius,
//             obstacleLayers,
//             QueryTriggerInteraction.Ignore);
//     }
//
//     
//     bool CanMemberPathToCurrentSlot(SquadMemberController member, Vector3 slot)
//     {
//         if (member == null || member.Agent == null || !member.Agent.enabled)
//             return false;
//
//         Vector3 validSlot = GetNearestValidPointForSlot(slot);
//
//         NavMeshPath path = new NavMeshPath();
//         bool hasPath = NavMesh.CalculatePath(
//             member.transform.position,
//             validSlot,
//             NavMesh.AllAreas,
//             path);
//
//         return hasPath && path.status == NavMeshPathStatus.PathComplete;
//     }
//     
//
//     Vector3 GetNearestValidPointForSlot(Vector3 slot)
//     {
//         if (NavMesh.SamplePosition(
//                 slot,
//                 out NavMeshHit hit,
//                 navMeshSampleRadius,
//                 NavMesh.AllAreas))
//         {
//             return hit.position;
//         }
//
//         return slot;
//     }
//
//     #endregion
//
//     
//     #region Formation
//
//     void RebuildFormation()
//     {
//         CleanNullMembers();
//
//         float width = formationWidth > 0f
//             ? formationWidth
//             : Mathf.Max(1, members.Count) * defaultSpacing;
//
//         // SESSION: Squad Control
//         // if (FormationManager.Instance != null)
//         // {
//         //     formationOffsets = FormationManager.Instance.CalculateOffsets(
//         //         members.Count,
//         //         width,
//         //         formation);
//         // }
//         // else
//         // {
//             formationOffsets = BuildFallbackLineOffsets(members.Count);
//         // }
//
//         ReassignSlotIndices();
//     }
//
//     
//     List<Vector3> GetWorldSlots(Vector3 center, Vector3 slotFacing)
//     {
//         // SESSION: Squad Control
//         // if (FormationManager.Instance != null)
//         // {
//         //     return FormationManager.Instance.ConvertOffsetsToWorldPositions(
//         //         formationOffsets,
//         //         center,
//         //         slotFacing);
//         // }
//
//         List<Vector3> result = new List<Vector3>();
//         Vector3 right = new Vector3(slotFacing.z, 0f, -slotFacing.x).normalized;
//
//         foreach (Vector2 offset in formationOffsets)
//             result.Add(center + right * offset.x + slotFacing * offset.y);
//
//         return result;
//     }
//
//     
//     List<Vector2> BuildFallbackLineOffsets(int count)
//     {
//         List<Vector2> offsets = new List<Vector2>();
//         if (count <= 0) return offsets;
//
//         float rowWidth = (count - 1) * defaultSpacing;
//
//         for (int i = 0; i < count; i++)
//         {
//             float x = i * defaultSpacing - rowWidth / 2f;
//             offsets.Add(new Vector2(x, 0f));
//         }
//
//         return offsets;
//     }
//     
//     
//     #endregion
//
//     
//     #region Members
//     
//     
//     public bool CanAcceptMember(SquadMemberController member)
//     {
//         if (member == null) return false;
//         if (!HasRoom) return false;
//         return true;
//     }
//
//     
//     public bool AddMember(SquadMemberController member)
//     {
//         if (!CanAcceptMember(member)) return false;
//
//         if (member.Squad != null && member.Squad != this)
//             member.Squad.RemoveMember(member);
//
//         member.transform.SetParent(null, true);
//
//         members.Add(member);
//         member.JoinSquad(this, members.Count - 1);
//
//         RebuildFormation();
//         return true;
//     }
//     
//
//     public void RemoveMember(SquadMemberController member)
//     {
//         if (member == null) return;
//         if (!members.Contains(member)) return;
//
//         members.Remove(member);
//         member.LeaveSquad();
//
//         ReassignSlotIndices();
//         RebuildFormation();
//
//         if (members.Count == 0)
//             Destroy(gameObject);
//     }
//     
//
//     void ReassignSlotIndices()
//     {
//         for (int i = 0; i < members.Count; i++)
//             if (members[i] != null)
//                 members[i].SetSlotIndex(i);
//     }
//
//     
//     void CleanNullMembers()
//     {
//         members = members.Where(m => m != null).ToList();
//     }
//
//     
//     bool MembersHaveSameFaction(List<SquadMemberController> startingMembers)
//     {
//         FactionInstance faction = null;
//
//         foreach (SquadMemberController member in startingMembers)
//         {
//             if (member == null || member.Stats == null)
//                 continue;
//
//             if (faction == null)
//             {
//                 faction = member.Stats.faction;
//                 continue;
//             }
//
//             if (member.Stats.faction != faction)
//                 return false;
//         }
//
//         return true;
//     }
//     
//     #endregion
//
//     
//     #region Merge
//
//     public bool CanMergeWith(SquadController other)
//     {
//         if (other == null) return false;
//         if (other == this) return false;
//         if (other.Category != Category) return false;
//         if (members.Count + other.MemberCount > maxMembers) return false;
//
//         return true;
//     }
//
//     
//     public bool AbsorbSquad(SquadController other)
//     {
//         if (!CanMergeWith(other)) return false;
//
//         List<SquadMemberController> incoming = other.members.ToList();
//
//         foreach (SquadMemberController member in incoming)
//             AddMember(member);
//
//         other.members.Clear();
//         Destroy(other.gameObject);
//
//         RebuildFormation();
//         return true;
//     }
//
//     #endregion
//
//     
//     #region Facing
//
//     Vector3 ResolveFacing(Vector3 destination)
//     {
//         Vector3 dir = destination - transform.position;
//         dir.y = 0f;
//
//         if (dir == Vector3.zero)
//             return facing == Vector3.zero ? Vector3.forward : facing;
//
//         return dir.normalized;
//     }
//
//     
//     void UpdateFacingFromAgent()
//     {
//         if (squadAgent == null) return;
//
//         if (squadAgent.velocity.sqrMagnitude > 0.1f)
//         {
//             Vector3 dir = squadAgent.velocity;
//             dir.y = 0f;
//
//             if (dir != Vector3.zero)
//                 desiredFacing = dir.normalized;
//         }
//
//         SmoothFacingToward(desiredFacing);
//     }
//
//     
//     void SmoothFacingToward(Vector3 targetFacing)
//     {
//         targetFacing.y = 0f;
//         if (targetFacing == Vector3.zero) return;
//
//         facing = Vector3.RotateTowards(
//             facing,
//             targetFacing.normalized,
//             turnSpeed * Mathf.Deg2Rad * Time.deltaTime,
//             0f);
//     }
//
//     #endregion
//     
//     
// }