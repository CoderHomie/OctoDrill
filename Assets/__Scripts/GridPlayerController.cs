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

    public Vector2Int GridPosition { get; private set; }
    public Vector2Int LastMoveDirection { get; private set; }

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

    void Awake()
    {
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
        SnapTransformToGrid();
        LastMoveDirection = Vector2Int.right;
        ApplyFacing(LastMoveDirection);
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
            return;
        }

        _moveElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_moveElapsed / moveDuration);
        t = t * t * (3f - 2f * t);
        transform.position = Vector3.Lerp(_moveFromWorld, _moveToWorld, t);
        if (t >= 1f)
            _isMoving = false;
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

        _moveFromWorld = transform.position;
        _moveToWorld = CellCenterWorld(to);

        if (moveDuration <= 0f)
            transform.position = _moveToWorld;
        else
        {
            _isMoving = true;
            _moveElapsed = 0f;
        }

        return true;
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
