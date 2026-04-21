using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates UI Text for the current run score and persisted high score. High score uses PlayerPrefs (works in WebGL).
/// Place on a scene object (e.g. Canvas or empty "UI" object). Drag your <c>Score</c>, <c>High Score</c>, and optional <c>Level</c> Text components into the fields.
/// On the main menu, leave <see cref="scoreText"/> empty if you only want to show the high score line.
/// From gameplay, call <see cref="AddScore"/> or <see cref="TryAddScore"/>.
/// Call <see cref="TryAdvanceLevel"/> when a new grid round begins (e.g. after the drill / goal tile is reached).
/// </summary>
public class ScoreHud : MonoBehaviour
{
    public static ScoreHud Instance { get; private set; }

    const string HighScorePrefsKey = "OctoDrill_HighScore";

    [Header("UI")]
    [Tooltip("Drag the Text on your \"Score\" object. Optional on main menu if you only show high score.")]
    [SerializeField] Text scoreText;
    [Tooltip("Drag the Text on your \"High Score\" object.")]
    [SerializeField] Text highScoreText;

    [SerializeField] string scoreFormat = "Score: {0}";
    [SerializeField] string highScoreFormat = "High Score: {0}";

    [Tooltip("Drag a UI Text for the current round / grid level (starts at 1, increases after each drill goal).")]
    [SerializeField] Text levelText;
    [SerializeField] string levelFormat = "Level: {0}";

    [Header("Gameplay HUD row (optional)")]
    [Tooltip("Centers score, high score, and lives vertically between the screen top and the grid top.")]
    [SerializeField] Camera layoutCamera;
    [Tooltip("If empty, resolved at runtime.")]
    [SerializeField] GridPlayerController layoutPlayer;
    [Tooltip("RectTransform of the Lives text (same row as score / high score).")]
    [SerializeField] RectTransform livesHudRect;
    [SerializeField] float layoutHorizontalPadding = 20f;

    int _runScore;
    int _highScore;
    int _level = 1;

    public int RunScore => _runScore;
    public int HighScore => _highScore;
    public int CurrentLevel => _level;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _highScore = PlayerPrefs.GetInt(HighScorePrefsKey, 0);
        _runScore = 0;
        _level = 1;
        if (layoutCamera == null)
            layoutCamera = Camera.main;
        if (layoutPlayer == null)
            layoutPlayer = FindFirstObjectByType<GridPlayerController>();
        RefreshUI();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        if (!isActiveAndEnabled || !Application.isPlaying)
            return;
        ApplyGameplayHudRowLayout();
    }

    void ApplyGameplayHudRowLayout()
    {
        if (scoreText == null || highScoreText == null || livesHudRect == null)
            return;
        if (layoutCamera == null || layoutPlayer == null)
            return;

        Canvas canvas = scoreText.canvas;
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return;

        float topGridWorldY = layoutPlayer.WorldYTopOfGrid();
        float z = Mathf.Abs(layoutPlayer.transform.position.z - layoutCamera.transform.position.z);
        if (z < 0.01f)
            z = 10f;

        Vector3 vp = layoutCamera.WorldToViewportPoint(
            new Vector3(layoutCamera.transform.position.x, topGridWorldY, z));

        float hudLineY = (1f + vp.y) * 0.5f;
        hudLineY = Mathf.Clamp01(hudLineY);

        RectTransform scoreRt = scoreText.rectTransform;
        RectTransform highRt = highScoreText.rectTransform;

        PlaceOnLine(scoreRt, new Vector2(0f, hudLineY), new Vector2(0f, 0.5f), new Vector2(layoutHorizontalPadding, 0f));
        PlaceOnLine(highRt, new Vector2(0.5f, hudLineY), new Vector2(0.5f, 0.5f), Vector2.zero);
        PlaceOnLine(livesHudRect, new Vector2(1f, hudLineY), new Vector2(1f, 0.5f), new Vector2(-layoutHorizontalPadding, 0f));
    }

    static void PlaceOnLine(RectTransform rt, Vector2 anchorNorm, Vector2 pivot, Vector2 anchoredOffset)
    {
        rt.anchorMin = anchorNorm;
        rt.anchorMax = anchorNorm;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredOffset;
    }

    /// <summary>Add points to the current run; updates high score and PlayerPrefs when exceeded.</summary>
    public void AddScore(int delta)
    {
        if (delta == 0)
            return;

        _runScore = Mathf.Max(0, _runScore + delta);
        if (_runScore > _highScore)
        {
            _highScore = _runScore;
            PlayerPrefs.SetInt(HighScorePrefsKey, _highScore);
            PlayerPrefs.Save();
        }

        RefreshUI();
    }

    /// <summary>Same as <see cref="AddScore"/> but safe if no <see cref="ScoreHud"/> is in the loaded scene.</summary>
    public static void TryAddScore(int delta)
    {
        if (Instance != null)
            Instance.AddScore(delta);
    }

    /// <summary>Increase level by 1 after the player hits the drill / goal tile and the grid round respawns.</summary>
    public void AdvanceLevel()
    {
        _level++;
        RefreshUI();
    }

    /// <summary>Same as <see cref="AdvanceLevel"/> when a <see cref="ScoreHud"/> may be absent.</summary>
    public static void TryAdvanceLevel()
    {
        if (Instance != null)
            Instance.AdvanceLevel();
    }

    /// <summary>Reset the run score to 0 (high score unchanged).</summary>
    public void ResetRunScore()
    {
        _runScore = 0;
        RefreshUI();
    }

    /// <summary>Re-read high score from PlayerPrefs (e.g. after clearing saved data elsewhere).</summary>
    public void ReloadHighScoreFromStorage()
    {
        _highScore = PlayerPrefs.GetInt(HighScorePrefsKey, 0);
        RefreshUI();
    }

    void RefreshUI()
    {
        if (scoreText != null)
            scoreText.text = string.Format(scoreFormat, _runScore);
        if (highScoreText != null)
            highScoreText.text = string.Format(highScoreFormat, _highScore);
        if (levelText != null)
            levelText.text = string.Format(levelFormat, _level);
    }
}
