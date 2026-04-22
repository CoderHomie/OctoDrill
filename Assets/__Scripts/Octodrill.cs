using UnityEngine;

public class Main : MonoBehaviour
{
    [Header("Spawn Telegraph")]
    [SerializeField] Sprite preSpawnHazardSprite;
    [SerializeField] float preSpawnWarningSeconds = 1f;
    [SerializeField] Color preSpawnHazardColor = Color.white;
    [SerializeField] int preSpawnHazardSortingOrder = 10;

    [Header("Shark")]
    [SerializeField] GameObject sharkPrefab; 
    public float sharkSpawnPerSecond = 0.5f;
    private float nextSharkSpawnTime = 0f;
    public float sharkSpeed = 5f;

    [Header("Urchin")]
    [SerializeField] GameObject urchinPrefab; 
    public float urchinSpawnPerSecond = 0.5f;
    private float nextUrchinSpawnTime = 0f;
    public float urchinSpeed = 2f;
    public int numSpikes = 6; 
    float _spawnResumeTime;

    [Header("Difficulty Scaling (per level)")]
    [Tooltip("Multiplier applied to shark spawns/second each new level.")]
    [SerializeField] float sharkSpawnRateLevelMultiplier = 1.25f;
    [Tooltip("Multiplier applied to shark speed each new level.")]
    [SerializeField] float sharkSpeedLevelMultiplier = 1.10f;
    [Tooltip("Multiplier applied to urchin spawns/second each new level.")]
    [SerializeField] float urchinSpawnRateLevelMultiplier = 1.05f;
    [Tooltip("Upper cap for shark spawns/second after scaling.")]
    [SerializeField] float sharkSpawnRateMax = 1.5f;
    [Tooltip("Upper cap for shark speed after scaling.")]
    [SerializeField] float sharkSpeedMax = 7.5f;

    float _baseSharkSpawnPerSecond;
    float _baseSharkSpeed;
    float _baseUrchinSpawnPerSecond;

    [Header("Bounds")]
    [SerializeField] Vector2Int minCell = new Vector2Int(-8, -4);
    [SerializeField] Vector2Int maxCell = new Vector2Int(7, 3);


    // Update is called once per frame
    void Awake()
    {
        _baseSharkSpawnPerSecond = sharkSpawnPerSecond;
        _baseSharkSpeed = sharkSpeed;
        _baseUrchinSpawnPerSecond = urchinSpawnPerSecond;
    }

    void Update()
    {
        if (PlayerLivesManager.Instance != null && PlayerLivesManager.Instance.IsGameOver)
            return;
        if (Time.time < _spawnResumeTime)
            return;

        if (Time.time >= nextSharkSpawnTime)
        {
            QueueSharkSpawn();
            nextSharkSpawnTime = Time.time + (1f / sharkSpawnPerSecond);
        }

        if (Time.time >= nextUrchinSpawnTime)
        {
            QueueUrchinSpawn();
            nextUrchinSpawnTime = Time.time + (1f / urchinSpawnPerSecond);
        }
    }

    /// <summary>Prevents new enemies from spawning until the delay has elapsed.</summary>
    public void PauseSpawningForSeconds(float seconds)
    {
        float resumeAt = Time.time + Mathf.Max(0f, seconds);
        _spawnResumeTime = Mathf.Max(_spawnResumeTime, resumeAt);
        nextSharkSpawnTime = Mathf.Max(nextSharkSpawnTime, _spawnResumeTime);
        nextUrchinSpawnTime = Mathf.Max(nextUrchinSpawnTime, _spawnResumeTime);
    }

    /// <summary>Called when a new level/round begins; increases enemy pressure.</summary>
    public void AdvanceDifficultyForNewLevel()
    {
        sharkSpawnPerSecond *= Mathf.Max(1f, sharkSpawnRateLevelMultiplier);
        sharkSpawnPerSecond = Mathf.Min(sharkSpawnPerSecond, Mathf.Max(0.01f, sharkSpawnRateMax));

        sharkSpeed *= Mathf.Max(1f, sharkSpeedLevelMultiplier);
        sharkSpeed = Mathf.Min(sharkSpeed, Mathf.Max(0.01f, sharkSpeedMax));

        urchinSpawnPerSecond *= Mathf.Max(1f, urchinSpawnRateLevelMultiplier);
    }

    /// <summary>Restores spawn rates/speeds to their initial inspector values.</summary>
    public void ResetDifficultyToBase()
    {
        sharkSpawnPerSecond = _baseSharkSpawnPerSecond;
        sharkSpeed = _baseSharkSpeed;
        urchinSpawnPerSecond = _baseUrchinSpawnPerSecond;
    }

    struct SharkSpawnInfo
    {
        public Vector3 spawnPos;
        public Vector3 warningPos;
        public float direction;
        public bool spawnFromLeft;
    }

    struct UrchinSpawnInfo
    {
        public Vector3 spawnPos;
        public Vector3 warningPos;
        public UrchinEnemy.EntrySide entrySide;
    }

    void QueueSharkSpawn()
    {
        if (sharkPrefab == null)
            return;

        SharkSpawnInfo spawnInfo = BuildSharkSpawnInfo();
        StartCoroutine(SpawnSharkAfterWarning(spawnInfo));
    }

    void QueueUrchinSpawn()
    {
        if (urchinPrefab == null)
            return;

        UrchinSpawnInfo spawnInfo = BuildUrchinSpawnInfo();
        StartCoroutine(SpawnUrchinAfterWarning(spawnInfo));
    }

    SharkSpawnInfo BuildSharkSpawnInfo()
    {
        // Random Y between bounds
        int spawnY = Random.Range(minCell.y, maxCell.y + 1);
        
        // Random choice: spawn left or right, off-screen
        bool spawnFromLeft = Random.value > 0.5f;
        int spawnX;
        float direction;
                
        if (spawnFromLeft){
            spawnX = minCell.x - 2;
            direction = 1f;
            }
        else{
            spawnX = maxCell.x + 2;
            direction = -1f;
        }

        SharkSpawnInfo info = new SharkSpawnInfo
        {
            spawnPos = CellToWorldPosition(new Vector2Int(spawnX, spawnY)),
            warningPos = CellToWorldPosition(new Vector2Int(spawnFromLeft ? minCell.x : maxCell.x, spawnY)),
            direction = direction,
            spawnFromLeft = spawnFromLeft
        };
        return info;
    }

    UrchinSpawnInfo BuildUrchinSpawnInfo()
    {
        bool spawnFromLeftOrRight = Random.value > 0.5f;
        bool spawnFromLeft = Random.value > 0.5f;
        Vector2Int spawnCell;
        UrchinEnemy.EntrySide entrySide;

        if (spawnFromLeftOrRight)
        {
            if (spawnFromLeft)
            {
                spawnCell = new Vector2Int(minCell.x - 2, Random.Range(minCell.y, maxCell.y + 1));
                entrySide = UrchinEnemy.EntrySide.Left;
            }
            else
            {
                spawnCell = new Vector2Int(maxCell.x + 2, Random.Range(minCell.y, maxCell.y + 1));
                entrySide = UrchinEnemy.EntrySide.Right;
            }
        }
        else
        {
            bool spawnFromTop = Random.value > 0.5f;

            if (spawnFromTop)
            {
                spawnCell = new Vector2Int(Random.Range(minCell.x, maxCell.x + 1), maxCell.y + 2);
                entrySide = UrchinEnemy.EntrySide.Top;
            }
            else
            {
                spawnCell = new Vector2Int(Random.Range(minCell.x, maxCell.x + 1), minCell.y - 2);
                entrySide = UrchinEnemy.EntrySide.Bottom;
            }
        }

        UrchinSpawnInfo info = new UrchinSpawnInfo
        {
            spawnPos = CellToWorldPosition(spawnCell),
            warningPos = CellToWorldPosition(GetFirstInBoundsCellForEntry(spawnCell, entrySide)),
            entrySide = entrySide
        };
        return info;
    }

    System.Collections.IEnumerator SpawnSharkAfterWarning(SharkSpawnInfo spawnInfo)
    {
        SpawnPreWarningHazard(spawnInfo.warningPos);
        yield return WaitForPreSpawnWarning();
        yield return WaitForSpawnResumeWindow();

        if (this == null || !isActiveAndEnabled)
            yield break;
        if (PlayerLivesManager.Instance != null && PlayerLivesManager.Instance.IsGameOver)
            yield break;
        if (sharkPrefab == null)
            yield break;

        GameObject shark = Instantiate(sharkPrefab, spawnInfo.spawnPos, Quaternion.identity);
        SharkEnemy sharkEnemy = shark.GetComponent<SharkEnemy>();
        SpriteRenderer spriteRend = shark.GetComponentInChildren<SpriteRenderer>();

        if (sharkEnemy != null)
            sharkEnemy.sharkSpeed = sharkSpeed * spawnInfo.direction;

        if(!spawnInfo.spawnFromLeft && spriteRend != null)
            {
                spriteRend.flipX = true;
            }
    }

    System.Collections.IEnumerator SpawnUrchinAfterWarning(UrchinSpawnInfo spawnInfo)
    {
        SpawnPreWarningHazard(spawnInfo.warningPos);
        yield return WaitForPreSpawnWarning();
        yield return WaitForSpawnResumeWindow();

        if (this == null || !isActiveAndEnabled)
            yield break;
        if (PlayerLivesManager.Instance != null && PlayerLivesManager.Instance.IsGameOver)
            yield break;
        if (urchinPrefab == null)
            yield break;

        GameObject urchin = Instantiate(urchinPrefab, spawnInfo.spawnPos, Quaternion.identity);

        UrchinEnemy urchinEnemy = urchin.GetComponent<UrchinEnemy>();
        if (urchinEnemy != null)
        {
            urchinEnemy.urchinSpeed = urchinSpeed;
            urchinEnemy.entrySide = spawnInfo.entrySide;
            urchinEnemy.spikeCount = numSpikes;
        }

        SpriteRenderer spriteRend = urchin.GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null && spawnInfo.entrySide == UrchinEnemy.EntrySide.Left)
            spriteRend.flipX = true;
    }

    void SpawnPreWarningHazard(Vector3 spawnPos)
    {
        if (preSpawnHazardSprite == null)
            return;

        GameObject telegraph = new GameObject("SpawnWarning");
        telegraph.transform.position = spawnPos;

        SpriteRenderer renderer = telegraph.AddComponent<SpriteRenderer>();
        renderer.sprite = preSpawnHazardSprite;
        renderer.color = preSpawnHazardColor;
        renderer.sortingOrder = preSpawnHazardSortingOrder;

        if (preSpawnWarningSeconds > 0f && telegraph != null)
            Destroy(telegraph, preSpawnWarningSeconds);
    }

    WaitForSeconds WaitForPreSpawnWarning()
    {
        float waitSeconds = Mathf.Max(0f, preSpawnWarningSeconds);
        return new WaitForSeconds(waitSeconds);
    }

    System.Collections.IEnumerator WaitForSpawnResumeWindow()
    {
        while (Time.time < _spawnResumeTime)
            yield return null;
    }

    Vector2Int GetFirstInBoundsCellForEntry(Vector2Int spawnCell, UrchinEnemy.EntrySide entrySide)
    {
        switch (entrySide)
        {
            case UrchinEnemy.EntrySide.Left:
                return new Vector2Int(minCell.x, spawnCell.y);
            case UrchinEnemy.EntrySide.Right:
                return new Vector2Int(maxCell.x, spawnCell.y);
            case UrchinEnemy.EntrySide.Top:
                return new Vector2Int(spawnCell.x, maxCell.y);
            case UrchinEnemy.EntrySide.Bottom:
                return new Vector2Int(spawnCell.x, minCell.y);
            default:
                return spawnCell;
        }
    }

    Vector3 CellToWorldPosition(Vector2Int cell)
    {
        return new Vector3(cell.x, cell.y, 0);
    }

    //boundary check
    bool IsCellTraversable(Vector2Int cell)
    {
        bool insideX = cell.x >= minCell.x && cell.x <= maxCell.x;
        bool insideY = cell.y >= minCell.y && cell.y <= maxCell.y;
        return insideX && insideY;
    }
}
