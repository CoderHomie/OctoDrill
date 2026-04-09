using UnityEngine;

/// <summary>
/// Fills the orthographic camera view with a grid of tile prefabs for procedural scenes.
/// Run order is early so <see cref="TrashTileRevealer"/> can index spawned trash in <see cref="Awake"/>.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AutoTileBoardGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("If null, Camera.main is used.")]
    [SerializeField] Camera targetCamera;
    [Tooltip("Parent for spawned tiles (e.g. empty 'Tile Grid' object).")]
    [SerializeField] Transform tileParent;
    [SerializeField] GameObject trashTilePrefab;
    [SerializeField] GameObject whirlpoolTilePrefab;
    [SerializeField] GameObject netTilePrefab;

    [Header("Screen layout (world units, orthographic)")]
    [Tooltip("Reserved strip at the top of the view for UI (score).")]
    [SerializeField] float topMarginWorld = 1.25f;
    [SerializeField] float bottomMarginWorld = 0.1f;
    [SerializeField] float leftMarginWorld = 0.1f;
    [SerializeField] float rightMarginWorld = 0.1f;
    [Tooltip("Distance between grid cell centers (match GridPlayerController.cellSize).")]
    [SerializeField] float cellSize = 1f;
    [Tooltip("Gap between adjacent tile edges (tiles are scaled down inside each cell).")]
    [SerializeField] float gap = 0.12f;
    [SerializeField] float tileZ = -1f;

    [Header("Draw order vs Canvas background")]
    [Tooltip("Screen Space - Overlay always draws on top of the world; use Screen Space - Camera (or World Space) on your Canvas so tiles can sort above the background Image.")]
    [SerializeField] bool overrideTileSorting = true;
    [Tooltip("Must be a Sorting Layer that is listed AFTER your Canvas background layer in Edit > Project Settings > Tags and Layers > Sorting Layers (lower in list = drawn behind).")]
    [SerializeField] string tileSortingLayerName = "Default";
    [Tooltip("Higher draws in front within the same Sorting Layer.")]
    [SerializeField] int tileOrderInLayer = 10;
    [Tooltip("Must be greater than Tile Order In Layer, or tile sprites on the same cell will fully cover the player.")]
    [SerializeField] int playerOrderInLayer = 50;
    [Tooltip("Match the player sprite to the board so it is not hidden behind the background.")]
    [SerializeField] bool applySortingToPlayerSprite = true;

    [Header("Tile mix")]
    [SerializeField] [Range(0f, 1f)] float whirlpoolChance = 0.04f;
    [SerializeField] [Range(0f, 1f)] float netChance = 0.04f;
    [Tooltip("-1 = random each run; otherwise fixed seed.")]
    [SerializeField] int randomSeed = -1;

    [Header("Player")]
    [Tooltip("If set, applies grid origin and bounds so movement matches this board.")]
    [SerializeField] GridPlayerController player;
    [SerializeField] bool applyGridToPlayer = true;
    [SerializeField] Vector2Int playerStartCell = Vector2Int.zero;

    [Header("Cleanup")]
    [SerializeField] bool clearExistingChildrenOnGenerate = true;

    public Vector2 GridOriginWorld { get; private set; }
    public int Columns { get; private set; }
    public int Rows { get; private set; }

    /// <summary>Parent transform tiles are spawned under (falls back to this generator's transform).</summary>
    public Transform GetTileParentTransform() => tileParent != null ? tileParent : transform;

    void Awake()
    {
        Generate();
    }

    [ContextMenu("Generate (play mode)")]
    public void Generate()
    {
        if (trashTilePrefab == null || whirlpoolTilePrefab == null || netTilePrefab == null)
        {
            Debug.LogError($"{nameof(AutoTileBoardGenerator)}: assign all three tile prefabs.", this);
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            Debug.LogError($"{nameof(AutoTileBoardGenerator)}: need an orthographic camera (assign Target Camera or tag MainCamera).", this);
            return;
        }

        Transform parent = GetTileParentTransform();
        if (clearExistingChildrenOnGenerate)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        if (randomSeed >= 0)
            Random.InitState(randomSeed);

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        float bottomY = c.y - halfH + bottomMarginWorld;
        float topY = c.y + halfH - topMarginWorld;
        float leftX = c.x - halfW + leftMarginWorld;
        float rightX = c.x + halfW - rightMarginWorld;

        float availableW = Mathf.Max(0f, rightX - leftX);
        float availableH = Mathf.Max(0f, topY - bottomY);

        int cols = Mathf.Max(1, Mathf.FloorToInt(availableW / cellSize));
        int rows = Mathf.Max(1, Mathf.FloorToInt(availableH / cellSize));

        float gridW = cols * cellSize;
        float gridH = rows * cellSize;
        float startX = leftX + (availableW - gridW) * 0.5f;
        float startY = bottomY + (availableH - gridH) * 0.5f;

        GridOriginWorld = new Vector2(startX, startY);
        Columns = cols;
        Rows = rows;

        float tileSpan = Mathf.Max(0.01f, cellSize - gap);
        float refSize = GetUniformSpriteSize(trashTilePrefab);
        float scale = tileSpan / Mathf.Max(0.0001f, refSize);

        float cumulativeSpecial = Mathf.Clamp01(whirlpoolChance + netChance);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float r = Random.value;
                GameObject prefab;
                if (r < whirlpoolChance)
                    prefab = whirlpoolTilePrefab;
                else if (r < cumulativeSpecial)
                    prefab = netTilePrefab;
                else
                    prefab = trashTilePrefab;

                Vector3 pos = new Vector3(
                    startX + (x + 0.5f) * cellSize,
                    startY + (y + 0.5f) * cellSize,
                    tileZ);
                GameObject instance = Instantiate(prefab, pos, Quaternion.identity, parent);
                instance.transform.localScale = Vector3.one * scale;
                ApplyTileSorting(instance);
            }
        }

        if (applyGridToPlayer && player != null)
        {
            Vector2Int max = new Vector2Int(cols - 1, rows - 1);
            Vector2Int start = new Vector2Int(
                Mathf.Clamp(playerStartCell.x, 0, max.x),
                Mathf.Clamp(playerStartCell.y, 0, max.y));
            player.ApplyGeneratedGrid(GridOriginWorld, Vector2Int.zero, max, cellSize, start);
        }

        if (applySortingToPlayerSprite && player != null)
            ApplyPlayerSorting(player.gameObject);
    }

    void ApplyTileSorting(GameObject root)
    {
        if (!overrideTileSorting || root == null)
            return;

        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            sr.sortingLayerName = tileSortingLayerName;
            sr.sortingOrder = tileOrderInLayer;
        }
    }

    void ApplyPlayerSorting(GameObject root)
    {
        if (!overrideTileSorting || root == null)
            return;

        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
        {
            sr.sortingLayerName = tileSortingLayerName;
            sr.sortingOrder = playerOrderInLayer;
        }
    }

    static float GetUniformSpriteSize(GameObject prefab)
    {
        var sr = prefab.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
            return 1f;
        Vector3 s = sr.transform.localScale;
        Vector2 b = sr.sprite.bounds.size;
        float wx = Mathf.Abs(b.x * s.x);
        float wy = Mathf.Abs(b.y * s.y);
        return Mathf.Max(wx, wy);
    }
}
