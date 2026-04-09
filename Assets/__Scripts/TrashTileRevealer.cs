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
    [Header("References")]
    [SerializeField] GridPlayerController player;
    [Tooltip("Parent transform that contains all trash tile SpriteRenderers (for example: Trash Grid).")]
    [SerializeField] Transform trashRoot;
    [Tooltip("Prefab spawned after all trash is cleared. Use a simple sprite marker with no collider.")]
    [SerializeField] GameObject goalPrefab;

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

    readonly Dictionary<Vector2Int, GameObject> _trashTilesByCell = new Dictionary<Vector2Int, GameObject>();
    readonly List<GameObject> _allTrashTiles = new List<GameObject>();
    GameObject _goalInstance;
    bool _goalActive;
    Vector2Int _activeGoalCell;

    void Awake()
    {
        if (player == null)
            player = GetComponent<GridPlayerController>();

        RebuildTrashIndex();
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

        if (_trashTilesByCell.Count == 0)
            SpawnGoal(cell);
    }

    void SpawnGoal(Vector2Int lastClearedCell)
    {
        if (goalPrefab == null || player == null || _goalActive)
            return;

        if (spawnGoalAtRandomCell)
            _activeGoalCell = PickRandomGoalCell();
        else if (spawnGoalAtLastClearedCell)
            _activeGoalCell = lastClearedCell;
        else
            _activeGoalCell = goalSpawnCell;

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

        if (revealStartingCell && player != null)
            RevealCell(player.GridPosition);
    }
}
