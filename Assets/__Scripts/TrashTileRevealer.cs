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
    [Tooltip("Alternative to Trash Root: assign the same GameObject here if the Transform slot won't accept your drag.")]
    [SerializeField] GameObject trashRootObject;
    [Tooltip("If Trash Root is empty, uses the tile parent from AutoTileBoardGenerator (good when the Player is a prefab and scene references cannot be saved on the asset).")]
    [SerializeField] AutoTileBoardGenerator proceduralBoard;

    [Header("Behavior")]
    [Tooltip("If true, destroys trash tile gameobjects; otherwise just deactivates them.")]
    [SerializeField] bool destroyTrashTiles = true;
    [Tooltip("Reveal the starting tile on scene start.")]
    [SerializeField] bool revealStartingCell = true;
    [Tooltip("Tiles with this tag are ignored by trash reveal/removal.")]
    [SerializeField] string ignoreTag = "whirlpool";

    readonly Dictionary<Vector2Int, GameObject> _trashTilesByCell = new Dictionary<Vector2Int, GameObject>();

    void Awake()
    {
        if (player == null)
            player = GetComponent<GridPlayerController>();

        ResolveTrashRoot();
        RebuildTrashIndex();
    }

    void ResolveTrashRoot()
    {
        if (trashRoot == null && proceduralBoard != null)
            trashRoot = proceduralBoard.GetTileParentTransform();
        if (trashRoot == null && trashRootObject != null)
            trashRoot = trashRootObject.transform;
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
            if (!string.IsNullOrEmpty(ignoreTag) && sr.gameObject.tag == ignoreTag)
                continue;

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
