using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tracks lives for Doc. On fatal damage: loses a life and respawns at level start, or game over at 0 lives.
/// Place on a scene object that is not the player (e.g. camera or empty "Game" object).
/// </summary>
public class PlayerLivesManager : MonoBehaviour
{
    public static PlayerLivesManager Instance { get; private set; }

    [SerializeField] int startingLives = 3;
    [Tooltip("If empty, the first GridPlayerController in the scene is used at runtime.")]
    [SerializeField] GridPlayerController player;
    [Tooltip("Shown when lives reach 0 (optional).")]
    [SerializeField] GameObject gameOverRoot;
    [SerializeField] bool pauseTimeOnGameOver = true;

    [Header("UI")]
    [Tooltip("Drag a UI Text (uGUI) element here. For TextMeshPro, use a UI Text or add a small bridge script.")]
    [SerializeField] Text livesText;
    [Tooltip("e.g. \"Lives: {0}\" — {0} is replaced with the current count.")]
    [SerializeField] string livesTextFormat = "Lives: {0}";

    [Header("Scenes (buttons)")]
    [Tooltip("Must match the scene name in Build Settings (e.g. MainMenu).")]
    [SerializeField] string mainMenuSceneName = "MainMenu";

    int _lives;
    bool _gameOver;
    GridPlayerController _trackedPlayer;

    public int LivesRemaining => _lives;
    public bool IsGameOver => _gameOver;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _lives = Mathf.Max(0, startingLives);
        if (gameOverRoot != null)
            gameOverRoot.SetActive(false);
        RefreshLivesDisplay();
    }

    void Start()
    {
        _trackedPlayer = player != null ? player : FindFirstObjectByType<GridPlayerController>();
        RefreshLivesDisplay();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterPlayerDeath(GridPlayerController victim)
    {
        if (_gameOver || victim == null)
            return;
        if (_trackedPlayer == null)
            _trackedPlayer = victim;
        if (victim != _trackedPlayer)
            return;

        _lives = Mathf.Max(0, _lives - 1);
        ClearSpawnedEnemies();
        RefreshLivesDisplay();

        if (_lives <= 0)
        {
            _gameOver = true;
            if (pauseTimeOnGameOver)
                Time.timeScale = 0f;
            if (gameOverRoot != null)
                gameOverRoot.SetActive(true);
            Destroy(victim.gameObject);
            return;
        }

        victim.RespawnAtLevelStart();
        var revealer = victim.GetComponent<TrashTileRevealer>();
        if (revealer != null)
            revealer.ApplyRevealAfterRespawn();
    }

    /// <summary>Hook a UI button to return to gameplay or reload the current scene.</summary>
    public void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Hook a UI button (e.g. on game over) to return to the main menu.</summary>
    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void RefreshLivesDisplay()
    {
        if (livesText == null)
            return;
        livesText.text = string.Format(livesTextFormat, _lives);
    }

    static void ClearSpawnedEnemies()
    {
        var sharks = FindObjectsByType<SharkEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sharks.Length; i++)
        {
            if (sharks[i] != null)
                Destroy(sharks[i].gameObject);
        }

        var urchins = FindObjectsByType<UrchinEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < urchins.Length; i++)
        {
            if (urchins[i] != null)
                Destroy(urchins[i].gameObject);
        }

        var spike = FindObjectsByType<Spike>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < spike.Length; i++)
        {
            if (spike[i] != null)
                Destroy(spike[i].gameObject);
        }
    }
}
