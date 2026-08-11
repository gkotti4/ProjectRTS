using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight Battle Mode tactical minimap.
///
/// The minimap owns its simple squad markers directly:
/// - one dynamically-created Image per battle squad
/// - Dictionary maps each SquadController to its marker RectTransform
/// - BattleMap converts squad world positions into normalized map positions
///
/// No marker prefab or separate marker component is needed for the MVP.
/// </summary>
[DisallowMultipleComponent]
public class MinimapPanelUI : MonoBehaviour
{
    #region References

    [Header("Battle")]
    [SerializeField] private BattleGameModeController battleGameModeController;
    [SerializeField] private BattleMap battleMap;

    [Header("Map")]
    [Tooltip("RectTransform representing the usable inside area of the minimap.")]
    [SerializeField] private RectTransform minimapContent;

    #endregion

    #region Tuning

    [Header("Markers")]
    [SerializeField] private Vector2 minimapMarkerSize =
        new Vector2(7.5f, 7.5f);

    [SerializeField] private Color minimapPlayerMarkerColor =
        new Color(0.2f, 0.75f, 1f, 1f);

    [SerializeField] private Color minimapEnemyMarkerColor =
        new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Refresh")]
    [Tooltip("How often squad marker positions refresh. The minimap does not need to update every frame.")]
    [Min(0.05f)]
    [SerializeField] private float minimapRefreshInterval = 0.25f;

    #endregion

    #region Runtime

    private readonly Dictionary<SquadController, RectTransform> squadMarkers =
        new Dictionary<SquadController, RectTransform>();

    private float minimapRefreshTimer = 0f;
    private bool isSubscribedToBattleController = false;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (minimapContent == null)
            minimapContent = transform as RectTransform;
    }

    void OnEnable()
    {
        ResolveReferences();
        SubscribeToBattleController();
    }

    void Start()
    {
        ResolveReferences();
        SubscribeToBattleController();
        TryBuildFromExistingBattle();

        minimapRefreshTimer = 0f;
    }

    void Update()
    {
        minimapRefreshTimer -= Time.unscaledDeltaTime;

        if (minimapRefreshTimer > 0f)
            return;

        minimapRefreshTimer = Mathf.Max(
            0.02f,
            minimapRefreshInterval);

        RefreshMarkerPositions();
    }

    void OnDisable()
    {
        UnsubscribeFromBattleController();
    }

    void OnDestroy()
    {
        UnsubscribeFromBattleController();
        ClearMarkers();
    }

    void OnValidate()
    {
        minimapMarkerSize.x = Mathf.Max(1f, minimapMarkerSize.x);
        minimapMarkerSize.y = Mathf.Max(1f, minimapMarkerSize.y);
        minimapRefreshInterval = Mathf.Max(0.02f, minimapRefreshInterval);
    }

    #endregion

    #region Battle Binding

    void ResolveReferences()
    {
        if (battleGameModeController == null)
            battleGameModeController = BattleGameModeController.Instance;

        if (battleMap == null)
            battleMap = BattleMap.Instance;
    }

    void SubscribeToBattleController()
    {
        if (battleGameModeController == null ||
            isSubscribedToBattleController)
        {
            return;
        }

        battleGameModeController.OnArmiesSpawned += HandleArmiesSpawned;
        isSubscribedToBattleController = true;
    }

    void UnsubscribeFromBattleController()
    {
        if (battleGameModeController == null ||
            !isSubscribedToBattleController)
        {
            return;
        }

        battleGameModeController.OnArmiesSpawned -= HandleArmiesSpawned;
        isSubscribedToBattleController = false;
    }

    void HandleArmiesSpawned(
        IReadOnlyList<SquadController> playerArmy,
        IReadOnlyList<SquadController> enemyArmy)
    {
        BuildMarkers(playerArmy, enemyArmy);
    }

    void TryBuildFromExistingBattle()
    {
        if (battleGameModeController == null)
            return;

        if (battleGameModeController.PlayerSquads.Count == 0 &&
            battleGameModeController.EnemySquads.Count == 0)
        {
            return;
        }

        BuildMarkers(
            battleGameModeController.PlayerSquads,
            battleGameModeController.EnemySquads);
    }

    #endregion

    #region Marker Construction

    void BuildMarkers(
        IReadOnlyList<SquadController> playerArmy,
        IReadOnlyList<SquadController> enemyArmy)
    {
        ClearMarkers();

        BuildArmyMarkers(
            playerArmy,
            minimapPlayerMarkerColor);

        BuildArmyMarkers(
            enemyArmy,
            minimapEnemyMarkerColor);

        RefreshMarkerPositions();
    }

    void BuildArmyMarkers(
        IReadOnlyList<SquadController> army,
        Color markerColor)
    {
        if (army == null || minimapContent == null)
            return;

        for (int index = 0; index < army.Count; index++)
        {
            SquadController squad = army[index];

            if (squad == null || squadMarkers.ContainsKey(squad))
                continue;

            RectTransform marker =
                CreateMarker(squad, markerColor);

            squadMarkers.Add(squad, marker);
        }
    }

    RectTransform CreateMarker(
        SquadController squad,
        Color markerColor)
    {
        GameObject markerObject = new GameObject(
            $"MinimapMarker_{squad.name}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform markerRect =
            markerObject.GetComponent<RectTransform>();

        markerRect.SetParent(minimapContent, false);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = minimapMarkerSize;

        Image markerImage = markerObject.GetComponent<Image>();
        markerImage.color = markerColor;
        markerImage.raycastTarget = false;

        return markerRect;
    }

    void ClearMarkers()
    {
        foreach (KeyValuePair<SquadController, RectTransform> pair in squadMarkers)
        {
            RectTransform marker = pair.Value;

            if (marker != null)
                Destroy(marker.gameObject);
        }

        squadMarkers.Clear();
    }

    #endregion

    #region Marker Positioning

    void RefreshMarkerPositions()
    {
        if (battleMap == null)
            battleMap = BattleMap.Instance;

        if (battleMap == null || minimapContent == null)
            return;

        Rect contentRect = minimapContent.rect;

        foreach (KeyValuePair<SquadController, RectTransform> pair in squadMarkers)
        {
            SquadController squad = pair.Key;
            RectTransform marker = pair.Value;

            if (marker == null)
                continue;

            bool isLiving =
                squad != null &&
                squad.Roster != null &&
                squad.Roster.HasLivingSoldiers;

            if (marker.gameObject.activeSelf != isLiving)
                marker.gameObject.SetActive(isLiving);

            if (!isLiving)
                continue;

            Vector2 normalizedPosition =
                battleMap.WorldToNormalizedPosition(
                    squad.transform.position);

            marker.anchoredPosition = new Vector2(
                Mathf.Lerp(
                    contentRect.xMin,
                    contentRect.xMax,
                    normalizedPosition.x),
                Mathf.Lerp(
                    contentRect.yMin,
                    contentRect.yMax,
                    normalizedPosition.y));
        }
    }

    #endregion
}
