using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WorkerData_",
    menuName = "Scriptable Objects/Economy/WorkerData")]
public class WorkerData : ScriptableObject
{
    [Header("Identity")]
    public string workerName = "Worker";
    public Sprite icon;

    [Header("Prefab")]
    public WorkerController prefab;

    [Header("Stats")]
    public HealthStats health = HealthStats.Default;
    public MovementStats movement = MovementStats.Default;
    public GatheringStats gathering;

    [Header("Commands")]
    public List<CommandData> commands = new List<CommandData>();

    [Header("Build Options")]
    public List<BuildOptionData> buildOptions = new List<BuildOptionData>();
}