using UnityEngine;

public class FactionOwner : MonoBehaviour, IFactionOwned
{
    [SerializeField] private bool defaultToPlayerFaction = true;

    public FactionInstance Faction { get; private set; }

    public void Initialize(FactionInstance faction)
    {
        Faction = faction;
    }

    void Start()
    {
        if (Faction != null)
            return;

        if (GameManager.Instance == null)
            return;

        if (CompareTag("Enemy"))
        {
            Faction = GameManager.Instance.EnemyFaction;
            return;
        }

        if (defaultToPlayerFaction)
            Faction = GameManager.Instance.PlayerFaction;
    }
}