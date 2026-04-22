using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reveals coral by hiding trash tiles when the player steps onto a cell.
/// When all trash is cleared, spawns a goal; reaching it respawns trash for the next round.
/// Attach to the player (same object as GridPlayerController).
/// </summary>
public class TrashTileRevealer : MonoBehaviour
{
    static readonly Vector2Int[] CardinalDirs =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    [Header("References")]
    [SerializeField] GridPlayerController player;
    [Tooltip("Parent transform that contains all trash tile SpriteRenderers (for example: Trash Grid).")]
    [SerializeField] Transform trashRoot;
    [Tooltip("Prefab spawned after all trash is cleared. Use a simple sprite marker with no collider.")]
    [SerializeField] GameObject goalPrefab;

    [Header("Random hazards (each round)")]
    [Tooltip("Placed on a trash cell instead of trash when the roll hits (2% default). Needs whirlpool tag + trigger collider for teleport.")]
    [SerializeField] GameObject whirlpoolPrefab;
    [Tooltip("Placed on a trash cell instead of trash when the roll hits (2% default).")]
    [SerializeField] GameObject netPrefab;
    [Range(0f, 1f)]
    [SerializeField] float whirlpoolSpawnChance = 0.02f;
    [Range(0f, 1f)]
    [SerializeField] float netSpawnChance = 0.02f;
    [Tooltip("Rebuild skips renderers with this tag so spawned nets are not treated as trash.")]
    [SerializeField] string netIgnoreTag = "net";

    [Header("Behavior")]
    [Tooltip("Reveal the starting tile on scene start.")]
    [SerializeField] bool revealStartingCell = true;
    [Tooltip("Tiles with this tag are ignored by trash reveal/removal.")]
    [SerializeField] string ignoreTag = "whirlpool";
    [Tooltip("If true, goal spawns on a random cell inside the player's grid bounds (see Grid Player Controller min/max cell).")]
    [SerializeField] bool spawnGoalAtRandomCell = true;
    [Tooltip("Used when Random is off: fixed cell for the goal.")]
    [SerializeField] Vector2Int goalSpawnCell = new Vector2Int(15, 7);
    [Tooltip("If true (and Random is off), goal spawns where the final trash tile was cleared.")]
    [SerializeField] bool spawnGoalAtLastClearedCell;
    [Tooltip("Run score increase each time Doc clears a trash tile (see ScoreHud).")]
    [SerializeField] int scorePerTrashCleared = 100;
    [Tooltip("Delay before enemies begin spawning after Doc hits the drill tile and a new round starts.")]
    [SerializeField] float enemySpawnDelayAfterGoal = 2f;

    readonly Dictionary<Vector2Int, GameObject> _trashTilesByCell = new Dictionary<Vector2Int, GameObject>();
    readonly List<GameObject> _allTrashTiles = new List<GameObject>();
    readonly List<GameObject> _spawnedHazards = new List<GameObject>();
    GameObject _goalInstance;
    bool _goalActive;
    Vector2Int _activeGoalCell;

    void Awake()
    {
        if (player == null)
            player = GetComponent<GridPlayerController>();

        RebuildTrashIndex();
        ApplyRandomTileMix();
    }

    void OnEnable()
    {
        if (player != null)
            player.Moved += HandlePlayerMoved;
    }

    void OnDisable()
    {
        if (player != null)
            player.Moved -= HandlePlayerMoved;
    }

    IEnumerator Start()
    {
        yield return null;

        if (revealStartingCell && player != null)
            RevealCell(player.GridPosition);
    }

    /// <summary>Call after <see cref="GridPlayerController.RespawnAtLevelStart"/> so the starting cell clears like a new round.</summary>
    public void ApplyRevealAfterRespawn()
    {
        if (revealStartingCell && player != null)
            RevealCell(player.GridPosition);
    }

    void HandlePlayerMoved(Vector2Int from, Vector2Int to)
    {
        if (_goalActive && to == _activeGoalCell)
        {
            StartNextRound();
            return;
        }

        RevealCell(to);
    }

    void RebuildTrashIndex()
    {
        _trashTilesByCell.Clear();
        _allTrashTiles.Clear();

        if (player == null || trashRoot == null)
            return;

        var renderers = trashRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            if (!string.IsNullOrEmpty(ignoreTag) && sr.gameObject.CompareTag(ignoreTag))
                continue;
            if (!string.IsNullOrEmpty(netIgnoreTag) && sr.gameObject.CompareTag(netIgnoreTag))
                continue;

            Vector2Int cell = player.WorldToCell(sr.transform.position);
            if (!_trashTilesByCell.ContainsKey(cell))
                _trashTilesByCell.Add(cell, sr.gameObject);
            _allTrashTiles.Add(sr.gameObject);
        }
    }

    void RevealCell(Vector2Int cell)
    {
        if (!_trashTilesByCell.TryGetValue(cell, out GameObject tile) || tile == null)
            return;

        tile.SetActive(false);
        _trashTilesByCell.Remove(cell);

        if (scorePerTrashCleared != 0)
            ScoreHud.TryAddScore(scorePerTrashCleared);

        if (_trashTilesByCell.Count == 0)
            SpawnGoal(cell);
    }

    void SpawnGoal(Vector2Int lastClearedCell)
    {
        if (goalPrefab == null || player == null || _goalActive)
            return;

        ReplaceAllNetTilesWithCoral();

        if (spawnGoalAtRandomCell)
            _activeGoalCell = PickRandomGoalCell();
        else if (spawnGoalAtLastClearedCell)
            _activeGoalCell = lastClearedCell;
        else
            _activeGoalCell = goalSpawnCell;

        ClearHazardsAtCell(_activeGoalCell);

        Vector3 spawnWorld = player.CellCenterWorld(_activeGoalCell);

        _goalInstance = Instantiate(goalPrefab, spawnWorld, Quaternion.identity);
        _goalInstance.name = "RoundGoal";
        _goalActive = true;
    }

    /// <summary>Random cell inside player bounds, avoiding Doc's current cell when possible.</summary>
    Vector2Int PickRandomGoalCell()
    {
        Vector2Int min = player.MinCell;
        Vector2Int max = player.MaxCell;
        Vector2Int doc = player.GridPosition;

        int w = max.x - min.x + 1;
        int h = max.y - min.y + 1;
        int area = w * h;
        if (area <= 0)
            return doc;

        for (int i = 0; i < 64; i++)
        {
            int x = Random.Range(min.x, max.x + 1);
            int y = Random.Range(min.y, max.y + 1);
            var c = new Vector2Int(x, y);
            if (area > 1 && c == doc)
                continue;
            return c;
        }

        return new Vector2Int(Random.Range(min.x, max.x + 1), Random.Range(min.y, max.y + 1));
    }

    void StartNextRound()
    {
        PlayerLivesManager.ClearSpawnedEnemies();
        Main enemySpawner = FindFirstObjectByType<Main>();
        if (enemySpawner != null)
        {
            enemySpawner.AdvanceDifficultyForNewLevel();
            enemySpawner.PauseSpawningForSeconds(enemySpawnDelayAfterGoal);
        }
        DestroySpawnedHazards();

        if (_goalInstance != null)
            Destroy(_goalInstance);
        _goalInstance = null;
        _goalActive = false;

        for (int i = 0; i < _allTrashTiles.Count; i++)
        {
            if (_allTrashTiles[i] != null)
                _allTrashTiles[i].SetActive(true);
        }

        _trashTilesByCell.Clear();
        for (int i = 0; i < _allTrashTiles.Count; i++)
        {
            GameObject tile = _allTrashTiles[i];
            if (tile == null || !tile.activeInHierarchy || player == null)
                continue;

            Vector2Int cell = player.WorldToCell(tile.transform.position);
            if (!_trashTilesByCell.ContainsKey(cell))
                _trashTilesByCell.Add(cell, tile);
        }

        ApplyRandomTileMix();

        if (player != null)
            player.TeleportToViewportCenter();

        if (revealStartingCell && player != null)
            RevealCell(player.GridPosition);

        ScoreHud.TryAdvanceLevel();
        AwardBonusLifeEveryThirdClearedLevel();
    }

    void DestroySpawnedHazards()
    {
        for (int i = 0; i < _spawnedHazards.Count; i++)
        {
            if (_spawnedHazards[i] != null)
                Destroy(_spawnedHazards[i]);
        }

        _spawnedHazards.Clear();
    }

    /// <summary>
    /// Each indexed trash cell becomes trash (default), whirlpool, or net using independent probability bands.
    /// </summary>
    void ApplyRandomTileMix()
    {
        if (player == null || trashRoot == null || _trashTilesByCell.Count == 0)
            return;

        float w = Mathf.Clamp01(whirlpoolSpawnChance);
        float n = Mathf.Clamp01(netSpawnChance);
        float whirlpoolCutoff = w;
        float netCutoff = w + n;
        var blockedCells = new HashSet<Vector2Int>();

        var cells = new List<Vector2Int>(_trashTilesByCell.Keys);

        foreach (Vector2Int cell in cells)
        {
            if (!_trashTilesByCell.ContainsKey(cell))
                continue;

            float r = Random.value;
            GameObject prefab = null;
            if (r < whirlpoolCutoff)
                prefab = whirlpoolPrefab;
            else if (r < netCutoff)
                prefab = netPrefab;

            if (prefab == null)
                continue;
            if (!CanPlaceHazardWithoutBlockingReachability(cell, blockedCells))
                continue;

            _trashTilesByCell.Remove(cell);
            SetTrashVisualsActiveAtCell(cell, false);
            blockedCells.Add(cell);

            Vector3 pos = player.CellCenterWorld(cell);
            GameObject spawned = Instantiate(prefab, pos, Quaternion.identity, trashRoot);
            spawned.name = $"{prefab.name} ({cell.x},{cell.y})";
            _spawnedHazards.Add(spawned);
        }
    }

    bool CanPlaceHazardWithoutBlockingReachability(Vector2Int blockedCandidate, HashSet<Vector2Int> blockedCells)
    {
        Vector2Int min = player.MinCell;
        Vector2Int max = player.MaxCell;

        // Simulate this hazard and verify all remaining walkable cells stay connected.
        blockedCells.Add(blockedCandidate);
        bool reachable = AreAllUnblockedCellsConnected(min, max, blockedCells, player.GridPosition);
        blockedCells.Remove(blockedCandidate);
        return reachable;
    }

    bool AreAllUnblockedCellsConnected(
        Vector2Int min,
        Vector2Int max,
        HashSet<Vector2Int> blockedCells,
        Vector2Int preferredStart)
    {
        int totalUnblocked = 0;
        Vector2Int fallbackStart = preferredStart;
        bool foundFallback = false;

        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2Int c = new Vector2Int(x, y);
                if (blockedCells.Contains(c))
                    continue;
                totalUnblocked++;
                if (!foundFallback)
                {
                    fallbackStart = c;
                    foundFallback = true;
                }
            }
        }

        if (totalUnblocked <= 1)
            return true;

        Vector2Int start = preferredStart;
        bool preferredInBounds = preferredStart.x >= min.x && preferredStart.x <= max.x &&
                                 preferredStart.y >= min.y && preferredStart.y <= max.y;
        if (!preferredInBounds || blockedCells.Contains(preferredStart))
            start = fallbackStart;

        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < CardinalDirs.Length; i++)
            {
                Vector2Int next = current + CardinalDirs[i];
                if (next.x < min.x || next.x > max.x || next.y < min.y || next.y > max.y)
                    continue;
                if (blockedCells.Contains(next) || visited.Contains(next))
                    continue;
                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return visited.Count == totalUnblocked;
    }

    void SetTrashVisualsActiveAtCell(Vector2Int cell, bool active)
    {
        if (player == null)
            return;

        for (int i = 0; i < _allTrashTiles.Count; i++)
        {
            GameObject t = _allTrashTiles[i];
            if (t == null)
                continue;
            if (player.WorldToCell(t.transform.position) == cell)
                t.SetActive(active);
        }
    }

    void ReplaceAllNetTilesWithCoral()
    {
        if (player == null)
            return;

        for (int i = _spawnedHazards.Count - 1; i >= 0; i--)
        {
            GameObject hazard = _spawnedHazards[i];
            if (hazard == null)
            {
                _spawnedHazards.RemoveAt(i);
                continue;
            }

            if (!IsNetHazard(hazard))
                continue;

            Vector2Int cell = player.WorldToCell(hazard.transform.position);
            hazard.SetActive(false);
            Destroy(hazard);
            _spawnedHazards.RemoveAt(i);

            // Net replaced by coral: keep trash hidden at this cell.
            SetTrashVisualsActiveAtCell(cell, false);
            _trashTilesByCell.Remove(cell);
        }
    }

    void ClearHazardsAtCell(Vector2Int cell)
    {
        if (player == null)
            return;

        for (int i = _spawnedHazards.Count - 1; i >= 0; i--)
        {
            GameObject hazard = _spawnedHazards[i];
            if (hazard == null)
            {
                _spawnedHazards.RemoveAt(i);
                continue;
            }

            if (player.WorldToCell(hazard.transform.position) != cell)
                continue;

            hazard.SetActive(false);
            Destroy(hazard);
            _spawnedHazards.RemoveAt(i);
            SetTrashVisualsActiveAtCell(cell, false);
            _trashTilesByCell.Remove(cell);
        }
    }

    bool IsNetHazard(GameObject hazard)
    {
        if (hazard == null)
            return false;

        if (!string.IsNullOrEmpty(netIgnoreTag) && hazard.CompareTag(netIgnoreTag))
            return true;

        if (netPrefab != null && hazard.name.StartsWith(netPrefab.name))
            return true;

        return false;
    }

    /// <summary>Replaces only the net hazard at this cell with coral visuals. Returns true if a net was replaced.</summary>
    public bool ReplaceNetTileWithCoralAtCell(Vector2Int cell)
    {
        if (player == null)
            return false;

        for (int i = _spawnedHazards.Count - 1; i >= 0; i--)
        {
            GameObject hazard = _spawnedHazards[i];
            if (hazard == null)
            {
                _spawnedHazards.RemoveAt(i);
                continue;
            }

            if (!IsNetHazard(hazard))
                continue;
            if (player.WorldToCell(hazard.transform.position) != cell)
                continue;

            hazard.SetActive(false);
            Destroy(hazard);
            _spawnedHazards.RemoveAt(i);
            SetTrashVisualsActiveAtCell(cell, false);
            _trashTilesByCell.Remove(cell);
            return true;
        }

        return false;
    }

    void AwardBonusLifeEveryThirdClearedLevel()
    {
        if (ScoreHud.Instance == null || PlayerLivesManager.Instance == null)
            return;

        int clearedLevels = Mathf.Max(0, ScoreHud.Instance.CurrentLevel - 1);
        if (clearedLevels > 0 && clearedLevels % 3 == 0)
            PlayerLivesManager.Instance.AddLives(1);
    }
}
