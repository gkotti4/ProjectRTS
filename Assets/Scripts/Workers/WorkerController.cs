using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(FactionOwner))]
public class WorkerController : MonoBehaviour,
    ISelectable,
    IHoverable,
    ISelectionComparable,
    ICommandable,
    IFactionOwned
{
    [Header("Data")]
    [SerializeField] private WorkerData workerData;

    [Header("Visuals")]
    [SerializeField] private DecalProjector selectionDecal;
    [SerializeField] private GameObject hoverVisual;

    private NavMeshAgent agent;
    private FactionOwner factionOwner;
    private bool isSelected;

    public WorkerData Data => workerData;
    public FactionInstance Faction => factionOwner != null ? factionOwner.Faction : null;

    public SelectableKind SelectionKind => SelectableKind.Worker;
    public bool IsDragSelectable => true;
    public SelectableKind CommandKind => SelectableKind.Worker;
    public float DoubleClickSelectRange => 25f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        factionOwner = GetComponent<FactionOwner>();

        if (selectionDecal != null)
            selectionDecal.enabled = false;

        if (hoverVisual != null)
            hoverVisual.SetActive(false);

        if (workerData != null)
        {
            agent.speed = workerData.movement.moveSpeed;
            agent.acceleration = workerData.movement.acceleration > 0f
                ? workerData.movement.acceleration
                : 99999f;
        }

        agent.angularSpeed = 99999f;
        agent.autoBraking = false;
        agent.updateRotation = false;
    }

    void Start()
    {
        SelectionManager.Instance?.RegisterSelectable(this);
    }

    void Update()
    {
        RotateTowardVelocity();
    }

    void OnDestroy()
    {
        SelectionManager.Instance?.UnregisterSelectable(this);
    }

    public void OrderMove(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.stoppingDistance = 0.1f;
        agent.SetDestination(destination);
    }

    public void OrderStop()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.ResetPath();
    }

    public void OnSelect()
    {
        isSelected = true;

        if (selectionDecal != null)
            selectionDecal.enabled = true;
    }

    public void OnDeselect()
    {
        isSelected = false;

        if (selectionDecal != null)
            selectionDecal.enabled = false;

        if (hoverVisual != null)
            hoverVisual.SetActive(false);
    }

    public void OnHoverEnter()
    {
        if (hoverVisual != null)
            hoverVisual.SetActive(true);
    }

    public void OnHoverExit()
    {
        if (isSelected)
            return;

        if (hoverVisual != null)
            hoverVisual.SetActive(false);
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }

    public bool IsSameSelectionType(ISelectable other)
    {
        if (other is not WorkerController otherWorker)
            return false;

        return otherWorker.workerData == workerData;
    }

    public List<CommandData> GetCommands()
    {
        return workerData != null
            ? workerData.commands
            : new List<CommandData>();
    }

    void RotateTowardVelocity()
    {
        if (agent == null)
            return;

        Vector3 velocity = agent.velocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude < 0.01f)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(velocity.normalized, Vector3.up),
            workerData != null ? workerData.movement.turnSpeed * Time.deltaTime : 900f * Time.deltaTime);
    }
}