using UnityEngine;

public class UrchinEnemy : MonoBehaviour
{
    [Header("Urchin Attributes")]
    public float urchinSpeed = 2f;
    public float pauseDuration = 2f;
    public float enterStopLeftX = -2f;
     public float enterStopRightX = 2f;
    public float enterStopTopY = 2f;
    public float enterStopBottomY = -2f;
    public float offscreenMargin = 2f;
    public float minVisibleX = -8f;
    public float maxVisibleX = 7f;
    public float minVisibleY = -4f;
    public float maxVisibleY = 3f;

    public enum EntrySide { Left, Right, Top, Bottom }

    [Header("Spawn Side")]
    [SerializeField] public EntrySide entrySide;

    enum MoveState { Entering, Paused, Exiting }
    MoveState state = MoveState.Entering;
    float pauseTimer;

    [Header("Spike Attributes")]
    public GameObject spikePrefab;
    public int spikeCount = 6;
    public float spikeSpeed = 3f;
    public float startAngle = 0f;
    public float spikeAngleStep = 60f;
    bool hasFiredThisPause;

    public Vector3 pos
    {
        get { return transform.position; }
        set { transform.position = value; }
    }

    void Update()
    {
        switch (state)
        {
            case MoveState.Entering:
                MoveTowardCenter();

                if (ReachedPausePoint())
                {
                    state = MoveState.Paused;
                    pauseTimer = pauseDuration;
                }
                break;

            case MoveState.Paused:
                pauseTimer -= Time.deltaTime;

                if (!hasFiredThisPause)
                {
                    hasFiredThisPause = true;
                    FireSpikesBurst();;
                }

                if (pauseTimer <= 0f)
                {
                    state = MoveState.Exiting;
                }
                break;

            case MoveState.Exiting:
                MoveAwayFromCenter();

                if (IsOffscreen())
                    Destroy(gameObject);
                break;
        }
    }

    void MoveTowardCenter()
    {
        switch (entrySide)
        {
            case EntrySide.Left:
                MoveHorizontal(1f);
                break;
            case EntrySide.Right:
                MoveHorizontal(-1f);
                break;
            case EntrySide.Top:
                MoveVertical(-1f);
                break;
            case EntrySide.Bottom:
                MoveVertical(1f);
                break;
        }
    }

    void MoveAwayFromCenter()
    {
        switch (entrySide)
        {
            case EntrySide.Left:
                MoveHorizontal(-1f);
                break;
            case EntrySide.Right:
                MoveHorizontal(1f);
                break;
            case EntrySide.Top:
                MoveVertical(1f);
                break;
            case EntrySide.Bottom:
                MoveVertical(-1f);
                break;
        }
    }

    bool ReachedPausePoint()
    {
        switch (entrySide)
        {
            case EntrySide.Left:
                return transform.position.x >= enterStopLeftX;
            case EntrySide.Right:
                return transform.position.x <= enterStopRightX;
            case EntrySide.Top:
                return transform.position.y <= enterStopTopY;
            case EntrySide.Bottom:
                return transform.position.y >= enterStopBottomY;
            default:
                return false;
        }
    }

    bool IsOffscreen()
    {
        return transform.position.x < minVisibleX - offscreenMargin ||
               transform.position.x > maxVisibleX + offscreenMargin ||
               transform.position.y < minVisibleY - offscreenMargin ||
               transform.position.y > maxVisibleY + offscreenMargin;
    }

    void MoveHorizontal(float direction)
    {
        Vector3 tempPos = pos;
        tempPos.x += direction * urchinSpeed * Time.deltaTime;
        pos = tempPos;
    }

    void MoveVertical(float direction)
    {
        Vector3 tempPos = pos;
        tempPos.y += direction * urchinSpeed * Time.deltaTime;
        pos = tempPos;
    }

    void FireSpikesBurst()
    {
        if (spikePrefab == null || spikeCount <= 0)
            return;

        float spawnOffsetY = 0f; // adjust for sprite size
        Vector3 spawnPoint = transform.position + Vector3.up * spawnOffsetY;

        for (int i = 0; i < spikeCount; i++)
        {
            float angle = i * spikeAngleStep;   // first spike is always 0
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);

            // Angle 0 points upward from the urchin
            Vector2 dir = rot * Vector2.up;

            GameObject spikeObj = Instantiate(spikePrefab, spawnPoint, rot);

            Spike spike = spikeObj.GetComponent<Spike>();
            if (spike != null)
                spike.Initialize(dir, spikeSpeed);
            else
            {
                Rigidbody2D rb = spikeObj.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = dir * spikeSpeed;
            }
        }
    }
}