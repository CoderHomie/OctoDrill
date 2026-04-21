using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Discrete 4-way grid movement for OctoDrill. Attach to the player root; align <see cref="gridOrigin"/>
/// with your tilemap or level grid. Override <see cref="IsCellTraversable"/> or listen to
/// <see cref="Moved"/> to plug in collisions, enemies, and drill logic later.
/// </summary>
public class GridPlayerController : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] float cellSize = 1f;
    [Tooltip("World position of grid cell (0, 0).")]
    [SerializeField] Vector2 gridOrigin;
    [Tooltip("Starting cell when not inferring from the transform.")]
    [SerializeField] Vector2Int startingCell;
    [SerializeField] bool useTransformPositionAsStartingCell;
    [Header("Bounds")]
    [Tooltip("Minimum allowed cell coordinate (inclusive).")]
    [SerializeField] Vector2Int minCell = new Vector2Int(0, 0);
    [Tooltip("Maximum allowed cell coordinate (inclusive).")]
    [SerializeField] Vector2Int maxCell = new Vector2Int(15, 7);

    [Header("Movement")]
    [Tooltip("0 = teleport to the next cell instantly; >0 = slide over that many seconds.")]
    [SerializeField] float moveDuration = 0.1f;
    [Tooltip("Delay before repeating a step while the same direction is held.")]
    [SerializeField] float moveRepeatInitialDelay = 0.2f;
    [SerializeField] float moveRepeatInterval = 0.12f;

    [Header("Input (optional)")]
    [Tooltip("Drag: InputSystem_Actions → Player → Move. If empty, WASD / arrows / gamepad stick are used directly.")]
    [SerializeField] InputActionReference moveAction;
    [Header("Facing (optional)")]
    [Tooltip("Sprite to flip/rotate when movement direction changes. If empty, auto-finds the first child SpriteRenderer.")]
    [SerializeField] SpriteRenderer facingSprite;
    [Tooltip("When enabled, up/down movement rotates the sprite +/-90 degrees. Leave off for left/right-only facing.")]
    [SerializeField] bool faceVerticalWithRotation;
    [Header("Whirlpool Teleport")]
    [SerializeField] bool enableWhirlpoolTeleport = true;
    [SerializeField] string whirlpoolTag = "whirlpool";
    [Tooltip("Layers checked when looking for a whirlpool at the current cell. Leave as Everything to search all layers.")]
    [SerializeField] LayerMask whirlpoolLayerMask = ~0;
    [Tooltip("Prevents immediate re-teleport loops when landing on/near another whirlpool.")]
    [SerializeField] float whirlpoolTeleportCooldown = 0.15f;
    [Header("Net")]
    [Tooltip("If a collider with this tag overlaps the cell center after a step, the player is destroyed (same outcome as Enemy-layer contact).")]
    [SerializeField] string netTag = "net";

    public Vector2Int GridPosition { get; private set; }
    public Vector2Int LastMoveDirection { get; private set; }

    /// <summary>Inclusive grid bounds (same as movement limits).</summary>
    public Vector2Int MinCell => minCell;
    /// <summary>Inclusive grid bounds (same as movement limits).</summary>
    public Vector2Int MaxCell => maxCell;

    /// <summary>World-space Y of the top edge of the grid (above the highest row).</summary>
    public float WorldYTopOfGrid()
    {
        return gridOrigin.y + (maxCell.y + 1) * cellSize;
    }

    /// <summary>Fired after a successful step; args are (fromCell, toCell).</summary>
    public event Action<Vector2Int, Vector2Int> Moved;

    InputAction _move;
    bool _usingAssetAction;
    Vector2Int _heldDirection;
    bool _repeatArmed;
    float _nextRepeatTime;

    bool _isMoving;
    float _moveElapsed;
    Vector3 _moveFromWorld;
    Vector3 _moveToWorld;
    Quaternion _facingBaseLocalRotation;
    bool _hasFacingBaseRotation;
    float _nextAllowedWhirlpoolTeleportTime;
    readonly Collider2D[] _whirlpoolOverlapResults = new Collider2D[8];
    ContactFilter2D _whirlpoolContactFilter;
    Vector2Int _respawnCell;
    int _enemyLayerIndex = -1;

    void Awake()
    {
        _enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (moveAction != null && moveAction.action != null)
        {
            _move = moveAction.action;
            _usingAssetAction = true;
        }

        if (facingSprite == null)
            facingSprite = GetComponentInChildren<SpriteRenderer>();
        if (facingSprite != null)
        {
            _facingBaseLocalRotation = facingSprite.transform.localRotation;
            _hasFacingBaseRotation = true;
        }

        _whirlpoolContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = whirlpoolLayerMask,
            useTriggers = true
        };
    }

    void OnEnable()
    {
        if (_usingAssetAction)
            _move.Enable();
    }

    void OnDisable()
    {
        if (_usingAssetAction)
            _move.Disable();
    }

    void Start()
    {
        GridPosition = useTransformPositionAsStartingCell ? WorldToCell(transform.position) : startingCell;
        _respawnCell = GridPosition;
        SnapTransformToGrid();
        LastMoveDirection = Vector2Int.right;
        ApplyFacing(LastMoveDirection);
        ResolveLandingHazards();
    }

    /// <summary>Used by <see cref="PlayerLivesManager"/> after losing a life (not on final game over).</summary>
    public void RespawnAtLevelStart()
    {
        GridPosition = _respawnCell;
        _isMoving = false;
        _moveElapsed = 0f;
        _heldDirection = Vector2Int.zero;
        _repeatArmed = false;
        LastMoveDirection = Vector2Int.right;
        _nextAllowedWhirlpoolTeleportTime = 0f;
        SnapTransformToGrid();
        ApplyFacing(LastMoveDirection);
        ResolveLandingHazards();
    }

    /// <summary>
    /// Places Doc on the grid cell under the screen center (viewport 0.5, 0.5). Clamps to <see cref="MinCell"/>/<see cref="MaxCell"/>.
    /// Updates the life-respawn cell for this round. Uses <see cref="Camera.main"/> if <paramref name="cam"/> is null.
    /// </summary>
    public void TeleportToViewportCenter(Camera cam = null)
    {
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;

        float planeZ = transform.position.z;
        float zDist = Mathf.Abs(planeZ - cam.transform.position.z);
        if (zDist < 0.0001f)
            zDist = 10f;

        Vector3 world = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, zDist));
        world.z = planeZ;

        Vector2Int cell = WorldToCell(world);
        cell = new Vector2Int(
            Mathf.Clamp(cell.x, minCell.x, maxCell.x),
            Mathf.Clamp(cell.y, minCell.y, maxCell.y));

        GridPosition = cell;
        _respawnCell = cell;
        _isMoving = false;
        _moveElapsed = 0f;
        _heldDirection = Vector2Int.zero;
        _repeatArmed = false;
        LastMoveDirection = Vector2Int.right;
        _nextAllowedWhirlpoolTeleportTime = 0f;
        SnapTransformToGrid();
        ApplyFacing(LastMoveDirection);
        ResolveLandingHazards();
    }

    void HandleFatalHit()
    {
        if (PlayerLivesManager.Instance != null)
            PlayerLivesManager.Instance.RegisterPlayerDeath(this);
        else
            Destroy(gameObject);
    }

    void Update()
    {
        if (_isMoving)
        {
            TickSmoothMove();
            return;
        }

        Vector2Int intent = ReadMoveIntent();
        if (intent == Vector2Int.zero)
        {
            _heldDirection = Vector2Int.zero;
            _repeatArmed = false;
            return;
        }

        if (intent != _heldDirection)
        {
            _heldDirection = intent;
            _repeatArmed = false;
        }

        if (!_repeatArmed)
        {
            if (TryStep(_heldDirection))
                ArmRepeatTimer();
        }
        else if (Time.time >= _nextRepeatTime)
        {
            TryStep(_heldDirection);
            _nextRepeatTime = Time.time + moveRepeatInterval;
        }
    }

    void ArmRepeatTimer()
    {
        _repeatArmed = true;
        _nextRepeatTime = Time.time + moveRepeatInitialDelay;
    }

    void TickSmoothMove()
    {
        if (moveDuration <= 0f)
        {
            transform.position = _moveToWorld;
            _isMoving = false;
            ResolveLandingHazards();
            return;
        }

        _moveElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_moveElapsed / moveDuration);
        t = t * t * (3f - 2f * t);
        transform.position = Vector3.Lerp(_moveFromWorld, _moveToWorld, t);
        if (t >= 1f)
        {
            _isMoving = false;
            ResolveLandingHazards();
        }
    }

    Vector2Int ReadMoveIntent()
    {
        Vector2 v = _usingAssetAction ? _move.ReadValue<Vector2>() : ReadFallbackMoveVector();
        return SnapToCardinal(v);
    }

    static Vector2 ReadFallbackMoveVector()
    {
        Vector2 v = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
        }

        if (v.sqrMagnitude < 0.01f)
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.2f)
                    v = stick;
            }
        }

        return v;
    }

    static Vector2Int SnapToCardinal(Vector2 v)
    {
        if (v.sqrMagnitude < 0.2f * 0.2f)
            return Vector2Int.zero;
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return new Vector2Int(v.x >= 0f ? 1 : -1, 0);
        return new Vector2Int(0, v.y >= 0f ? 1 : -1);
    }

    bool TryStep(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
            return false;

        Vector2Int from = GridPosition;
        Vector2Int to = from + direction;
        if (!IsCellTraversable(to))
            return false;

        GridPosition = to;
        LastMoveDirection = direction;
        ApplyFacing(direction);
        Moved?.Invoke(from, to);

        // Moved handlers may relocate the player (e.g. goal → next round teleport). Do not snap/lerp back to `to`.
        if (GridPosition != to)
        {
            _isMoving = false;
            _moveElapsed = 0f;
            SnapTransformToGrid();
            return true;
        }

        _moveFromWorld = transform.position;
        _moveToWorld = CellCenterWorld(to);

        if (moveDuration <= 0f)
        {
            transform.position = _moveToWorld;
            ResolveLandingHazards();
        }
        else
        {
            _isMoving = true;
            _moveElapsed = 0f;
        }

        return true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (other.gameObject.layer == enemyLayer)
        {
            HandleFatalHit();
            return;
        }

        if (!enableWhirlpoolTeleport || other == null)
            return;

        if (Time.time < _nextAllowedWhirlpoolTeleportTime)
            return;

        if (!other.CompareTag(whirlpoolTag))
            return;

        if (TryTeleportToRandomTraversableCell())
            _nextAllowedWhirlpoolTeleportTime = Time.time + Mathf.Max(0f, whirlpoolTeleportCooldown);
    }

    bool TryTeleportToRandomTraversableCell()
    {
        Vector2Int from = GridPosition;
        int width = maxCell.x - minCell.x + 1;
        int height = maxCell.y - minCell.y + 1;
        int totalCells = Mathf.Max(0, width * height);
        if (totalCells <= 0)
            return false;

        // Try a shuffled scan so teleport destination feels random but still reliable.
        int startIndex = UnityEngine.Random.Range(0, totalCells);
        for (int i = 0; i < totalCells; i++)
        {
            int index = (startIndex + i) % totalCells;
            int x = minCell.x + (index % width);
            int y = minCell.y + (index / width);
            Vector2Int candidate = new Vector2Int(x, y);

            if (candidate == from)
                continue;
            if (!IsCellTraversable(candidate))
                continue;
            if (CellCenterOverlapsNetOrEnemy(candidate))
                continue;

            GridPosition = candidate;
            _isMoving = false;
            _moveElapsed = 0f;
            _moveFromWorld = transform.position;
            _moveToWorld = CellCenterWorld(candidate);
            transform.position = _moveToWorld;
            Moved?.Invoke(from, candidate);
            return true;
        }

        return false;
    }

    /// <summary>True if the cell center overlaps a net (tag) or any collider on the Enemy layer — unsafe for whirlpool teleport.</summary>
    bool CellCenterOverlapsNetOrEnemy(Vector2Int cell)
    {
        Vector2 point = (Vector2)CellCenterWorld(cell);
        _whirlpoolContactFilter.layerMask = whirlpoolLayerMask;
        int hitCount = Physics2D.OverlapPoint(point, _whirlpoolContactFilter, _whirlpoolOverlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _whirlpoolOverlapResults[i];
            if (hit == null)
                continue;
            if (!string.IsNullOrEmpty(netTag) && hit.CompareTag(netTag))
                return true;
            if (_enemyLayerIndex >= 0 && hit.gameObject.layer == _enemyLayerIndex)
                return true;
        }

        return false;
    }

    void ResolveLandingHazards()
    {
        if (CheckNetAtCurrentCell())
            return;
        CheckWhirlpoolAtCurrentCell();
    }

    bool CheckNetAtCurrentCell()
    {
        if (string.IsNullOrEmpty(netTag))
            return false;

        Vector2 point = (Vector2)CellCenterWorld(GridPosition);
        _whirlpoolContactFilter.layerMask = whirlpoolLayerMask;
        int hitCount = Physics2D.OverlapPoint(point, _whirlpoolContactFilter, _whirlpoolOverlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _whirlpoolOverlapResults[i];
            if (hit == null || !hit.CompareTag(netTag))
                continue;

            HandleFatalHit();
            return true;
        }

        return false;
    }

    void CheckWhirlpoolAtCurrentCell()
    {
        if (!enableWhirlpoolTeleport)
            return;
        if (Time.time < _nextAllowedWhirlpoolTeleportTime)
            return;

        Vector2 point = (Vector2)CellCenterWorld(GridPosition);
        _whirlpoolContactFilter.layerMask = whirlpoolLayerMask;
        int hitCount = Physics2D.OverlapPoint(point, _whirlpoolContactFilter, _whirlpoolOverlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _whirlpoolOverlapResults[i];
            if (hit == null)
                continue;
            if (!hit.CompareTag(whirlpoolTag))
                continue;

            if (TryTeleportToRandomTraversableCell())
                _nextAllowedWhirlpoolTeleportTime = Time.time + Mathf.Max(0f, whirlpoolTeleportCooldown);
            break;
        }
    }

    void ApplyFacing(Vector2Int direction)
    {
        if (facingSprite == null || direction == Vector2Int.zero)
            return;

        if (direction.x != 0)
            facingSprite.flipX = direction.x < 0;
        else if (faceVerticalWithRotation && direction.y != 0)
            facingSprite.flipX = false;

        if (_hasFacingBaseRotation && faceVerticalWithRotation)
        {
            float zDegrees = 0f;
            if (direction.y > 0)
                zDegrees = 90f;
            else if (direction.y < 0)
                zDegrees = -90f;
            facingSprite.transform.localRotation = _facingBaseLocalRotation * Quaternion.Euler(0f, 0f, zDegrees);
        }
        else if (_hasFacingBaseRotation)
        {
            facingSprite.transform.localRotation = _facingBaseLocalRotation;
        }
    }

    /// <summary>Override in a subclass when you add tilemaps, walls, or entities.</summary>
    protected virtual bool IsCellTraversable(Vector2Int cell)
    {
        bool insideX = cell.x >= minCell.x && cell.x <= maxCell.x;
        bool insideY = cell.y >= minCell.y && cell.y <= maxCell.y;
        return insideX && insideY;
    }

    public Vector3 CellCenterWorld(Vector2Int cell)
    {
        float z = transform.position.z;
        return new Vector3(gridOrigin.x + (cell.x + 0.5f) * cellSize, gridOrigin.y + (cell.y + 0.5f) * cellSize, z);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        float fx = (world.x - gridOrigin.x) / cellSize;
        float fy = (world.y - gridOrigin.y) / cellSize;
        return new Vector2Int(Mathf.FloorToInt(fx), Mathf.FloorToInt(fy));
    }

    void SnapTransformToGrid()
    {
        transform.position = CellCenterWorld(GridPosition);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Vector3 center = new Vector3(gridOrigin.x + cellSize * 0.5f, gridOrigin.y + cellSize * 0.5f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(cellSize, cellSize, 0.1f));
    }
#endif
}
