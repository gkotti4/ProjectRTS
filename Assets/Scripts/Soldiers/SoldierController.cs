using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Collider))]
//[RequireComponent(typeof(SelectableProxy))]
[RequireComponent(typeof(SoldierHealth))]
[RequireComponent(typeof(SoldierMotor))]
[RequireComponent(typeof(SoldierCombat))]

public class SoldierController : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private GameObject selectionVisual;
    [SerializeField] private GameObject hoverVisual;

    public SoldierData Data { get; private set; }
    public SquadController Squad { get; private set; }
    public SquadRoster Roster { get; private set; }
    public FactionInstance Faction { get; private set; }
    
    public SoldierRole Role { get; private set; } = SoldierRole.None;
    public SoldierController CombatTarget { get; private set; }
    public SoldierCombat Combat { get; private set; }

    public SoldierHealth Health { get; private set; }
    public SoldierMotor Motor { get; private set; }
    public UnitAnimator UnitAnimator { get; private set; }

    public int SlotIndex { get; private set; } = -1;
    public Vector3 LastSlotPosition { get; private set; }

    public bool IsAlive => Health != null && Health.IsAlive;

    #region Unity Lifecycle

    void Awake()
    {
        Health = GetComponent<SoldierHealth>();
        Motor = GetComponent<SoldierMotor>();
        UnitAnimator = GetComponentInChildren<UnitAnimator>();
        Combat = GetComponent<SoldierCombat>();

        if (Combat == null)
            Debug.LogError("SoldierCombat is null.");

        SetSelectionVisual(false);
        SetHoverVisual(false);
    }

    void OnDestroy()
    {
        if (Health != null)
            Health.OnDied -= HandleDeath;
    }

    #endregion

    #region Initialization

    public void Initialize(
        SoldierData data,
        SquadController squad,
        SquadRoster roster,
        FactionInstance faction)
    {
        Data = data;
        Squad = squad;
        Roster = roster;
        Faction = faction;

        if (Data != null)
        {
            Health.Initialize(Data.health);
            Motor.Initialize(Data.movement);

            if (Data.animatorController != null && UnitAnimator != null)
            {
                Animator animator = UnitAnimator.GetComponent<Animator>();

                if (animator != null)
                    animator.runtimeAnimatorController = Data.animatorController;
            }
        }
        
        Combat.Initialize(this);

        Health.OnDied += HandleDeath;

        EnsureSelectableProxy();
    }

    public void SetSquad(
        SquadController squad,
        SquadRoster roster)
    {
        Squad = squad;
        Roster = roster;

        EnsureSelectableProxy();
    }
    
    /// Ensures this soldier redirects selection input to its owning squad.
    void EnsureSelectableProxy()
    {
        SelectionTarget proxy = GetComponentInChildren<SelectionTarget>();

        if (proxy == null)
            proxy = gameObject.AddComponent<SelectionTarget>();

        proxy.SetTarget(Squad);
    }

    #endregion

    #region Slot / Formation

    public void SetSlotIndex(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void SetLastSlotPosition(Vector3 position)
    {
        LastSlotPosition = position;
    }

    public void MoveToSlot(
        Vector3 slotPosition,
        float updateThreshold,
        float stoppingDistance = 0.1f,
        float speedMultiplier = 1f)
    {
        if (!IsAlive)
            return;

        if (!Calc.OutOfRange(LastSlotPosition, slotPosition, updateThreshold))
            return;

        Motor.MoveTo(
            slotPosition,
            stoppingDistance,
            speedMultiplier);

        LastSlotPosition = slotPosition;
    }

    public void MoveToPoint(
        Vector3 position,
        float stoppingDistance = 0.1f,
        float speedMultiplier = 1f)
    {
        if (!IsAlive)
            return;

        Motor.MoveTo(
            position,
            stoppingDistance,
            speedMultiplier);
    }

    public void Stop()
    {
        if (Motor == null)
            return;

        Motor.Stop();
    }

    #endregion

    #region Health / Death

    void HandleDeath(SoldierHealth health)
    {
        Stop();

        Role = SoldierRole.None;
        CombatTarget = null;
        
        Combat?.ClearCombat();

        if (UnitAnimator != null)
            UnitAnimator.TriggerDeath();

        Roster?.NotifySoldierDied(this);

        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
            col.enabled = false;
        
        Destroy(gameObject, 2f);
    }

    #endregion

    #region Visuals

    public void SetSelectionVisual(bool visible)
    {
        if (selectionVisual != null)
            selectionVisual.SetActive(visible);
    }

    public void SetHoverVisual(bool visible)
    {
        if (hoverVisual != null)
            hoverVisual.SetActive(visible);
    }

    #endregion
    
    #region Combat Visuals / Animation Hooks

    public void PlayAttackVisual()
    {
        if (UnitAnimator != null)
            UnitAnimator.TriggerAttack();
    }

    public void OnAttackImpact()
    {
        // Future animation-timed damage hook.
        // Damage is currently applied directly in SquadCombat.TryAttack().
    }

    public void OnAttackEnd()
    {
        // Future recovery / attack-state cleanup hook.
    }

    #endregion
    
    public void SetCombatRole(SoldierRole role)
    {
        Role = role;
    }

    public void SetCombatTarget(SoldierController target)
    {
        CombatTarget = target;
    }

    public void ClearCombatTarget()
    {
        CombatTarget = null;
    }

    public void MoveToCombatPoint(
        Vector3 position,
        float stoppingDistance,
        float speedMultiplier = 1f)
    {
        if (!IsAlive)
            return;

        Motor.MoveTo(position, stoppingDistance, speedMultiplier);
    }

    public void FaceToward(Vector3 position, float turnSpeed = 900f)
    {
        Vector3 dir = position - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            dir.normalized,
            Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);

        Motor?.SuppressVelocityRotation();
    }

}