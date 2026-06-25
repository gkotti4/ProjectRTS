using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance { get; private set; }

    private readonly List<SquadController> squads = new List<SquadController>();

    public IReadOnlyList<SquadController> Squads => squads;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterSquad(SquadController squad)
    {
        if (squad == null) return;
        if (squads.Contains(squad)) return;

        squads.Add(squad);
    }

    public void UnregisterSquad(SquadController squad)
    {
        if (squad == null) return;
        squads.Remove(squad);
    }

    public List<SquadController> GetSquadsByCategory(SquadCategory category)
    {
        return squads
            .Where(s => s != null && s.Category == category)
            .ToList();
    }
}

