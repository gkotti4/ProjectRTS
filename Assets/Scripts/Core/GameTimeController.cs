using System;
using UnityEngine;

/// -----------------------------------------------------------------------------
/// GameTimeController
/// -----------------------------------------------------------------------------
///
/// Central authority for gameplay time scale.
/// UI buttons, hotkeys, pause menus, and battle-result flow should use this instead
/// of writing Time.timeScale directly.
///
[DisallowMultipleComponent]
public class GameTimeController : MonoBehaviour
{
    public static GameTimeController Instance { get; private set; }

    public event Action<float> OnGameSpeedChanged;

    #region Tuning

    [Header("Game Speed")]
    [Min(0.01f)] [SerializeField] private float gameTimeNormalSpeed = 1f;
    [Min(0.01f)] [SerializeField] private float gameTimeFastSpeed = 2f;
    [Min(0.01f)] [SerializeField] private float gameTimeVeryFastSpeed = 3f;

    #endregion

    #region Runtime

    private float currentGameSpeed = 1f;
    private float lastNonZeroGameSpeed = 1f;

    public float CurrentGameSpeed => currentGameSpeed;
    public bool IsPaused => Mathf.Approximately(currentGameSpeed, 0f);

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        currentGameSpeed = Mathf.Max(0.01f, gameTimeNormalSpeed);
        lastNonZeroGameSpeed = currentGameSpeed;
        ApplyGameSpeed(currentGameSpeed, notify: false);
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        Time.timeScale = 1f;
    }

    void OnValidate()
    {
        gameTimeNormalSpeed = Mathf.Max(0.01f, gameTimeNormalSpeed);
        gameTimeFastSpeed = Mathf.Max(0.01f, gameTimeFastSpeed);
        gameTimeVeryFastSpeed = Mathf.Max(0.01f, gameTimeVeryFastSpeed);
    }

    #endregion

    #region Public API

    public void Pause()
    {
        if (IsPaused)
            return;

        lastNonZeroGameSpeed = Mathf.Max(0.01f, currentGameSpeed);
        ApplyGameSpeed(0f);
    }

    public void Resume()
    {
        if (!IsPaused)
            return;

        ApplyGameSpeed(Mathf.Max(0.01f, lastNonZeroGameSpeed));
    }

    public void TogglePause()
    {
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    public void SetNormalSpeed()
    {
        SetGameSpeed(gameTimeNormalSpeed);
    }

    public void SetFastSpeed()
    {
        SetGameSpeed(gameTimeFastSpeed);
    }

    public void SetVeryFastSpeed()
    {
        SetGameSpeed(gameTimeVeryFastSpeed);
    }

    public void SetGameSpeed(float speed)
    {
        speed = Mathf.Max(0f, speed);

        if (speed <= 0f)
        {
            Pause();
            return;
        }

        lastNonZeroGameSpeed = speed;
        ApplyGameSpeed(speed);
    }

    #endregion

    #region Internal

    void ApplyGameSpeed(float speed, bool notify = true)
    {
        currentGameSpeed = Mathf.Max(0f, speed);
        Time.timeScale = currentGameSpeed;

        if (notify)
            OnGameSpeedChanged?.Invoke(currentGameSpeed);
    }

    #endregion
}
