using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reveals coral by removing trash tiles when the player steps onto a cell.
/// Attach to the player (same object as GridPlayerController).
/// </summary>
public class TrashTileRevealer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GridPlayerController player;
    [Tooltip("Parent transform that contains all trash tile SpriteRenderers (for example: Trash Grid).")]
    [SerializeField] Transform trashRoot;

    [Header("Behavior")]
    [Tooltip("If true, destroys trash tile gameobjects; otherwise just deactivates them.")]
    [SerializeField] bool destroyTrashTiles = true;
    [Tooltip("Reveal the starting tile on scene start.")]
    [SerializeField] bool revealStartingCell = true;

    readonly Dictionary<Vector2Int, GameObject> _trashTilesByCell = new Dictionary<Vector2Int, GameObject>();

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
        // Wait one frame so GridPlayerController has initialized GridPosition.
        yield return null;

        if (revealStartingCell && player != null)
            RevealCell(player.GridPosition);
    }

    void HandlePlayerMoved(Vector2Int from, Vector2Int to)
    {
        RevealCell(to);
    }

    void RebuildTrashIndex()
    {
        _trashTilesByCell.Clear();

        if (player == null || trashRoot == null)
            return;

        var renderers = trashRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            Vector2Int cell = player.WorldToCell(sr.transform.position);
            if (!_trashTilesByCell.ContainsKey(cell))
                _trashTilesByCell.Add(cell, sr.gameObject);
        }
    }

    void RevealCell(Vector2Int cell)
    {
        if (!_trashTilesByCell.TryGetValue(cell, out GameObject tile) || tile == null)
            return;

        if (destroyTrashTiles)
            Destroy(tile);
        else
            tile.SetActive(false);

        _trashTilesByCell.Remove(cell);
    }
}
