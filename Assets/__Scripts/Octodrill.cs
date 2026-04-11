using UnityEngine;

public class Main : MonoBehaviour
{
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

    [Header("Bounds")]
    [SerializeField] Vector2Int minCell = new Vector2Int(-8, -4);
    [SerializeField] Vector2Int maxCell = new Vector2Int(7, 3);


    // Update is called once per frame
    void Update()
    {
        if (PlayerLivesManager.Instance != null && PlayerLivesManager.Instance.IsGameOver)
            return;

        if (Time.time >= nextSharkSpawnTime)
        {
            SpawnShark();
            nextSharkSpawnTime = Time.time + (1f / sharkSpawnPerSecond);
        }

        if (Time.time >= nextUrchinSpawnTime)
        {
            SpawnUrchin();
            nextUrchinSpawnTime = Time.time + (1f / urchinSpawnPerSecond);
        }
    }

    void SpawnShark()
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

        Vector3 spawnPos = CellToWorldPosition(new Vector2Int(spawnX, spawnY));
        GameObject shark = Instantiate(sharkPrefab, spawnPos, Quaternion.identity);
        SharkEnemy sharkEnemy = shark.GetComponent<SharkEnemy>();
        SpriteRenderer spriteRend = shark.GetComponentInChildren<SpriteRenderer>();

        sharkEnemy.sharkSpeed = sharkSpeed * direction;

        if(!spawnFromLeft && spriteRend != null)
            {
                spriteRend.flipX = true;
            }
    }

    void SpawnUrchin()
    {
        bool spawnFromLeftOrRight = Random.value > 0.5f;
        bool spawnFromLeft = Random.value > 0.5f;

        float direction;
        Vector2Int spawnCell;
        UrchinEnemy.EntrySide entrySide;

        if (spawnFromLeftOrRight)
        {
            if (spawnFromLeft)
            {
                spawnCell = new Vector2Int(minCell.x - 2, Random.Range(minCell.y, maxCell.y + 1));
                entrySide = UrchinEnemy.EntrySide.Left;
                direction = 1f;
            }
            else
            {
                spawnCell = new Vector2Int(maxCell.x + 2, Random.Range(minCell.y, maxCell.y + 1));
                entrySide = UrchinEnemy.EntrySide.Right;
                direction = -1f;
            }
        }
        else
        {
            bool spawnFromTop = Random.value > 0.5f;

            if (spawnFromTop)
            {
                spawnCell = new Vector2Int(Random.Range(minCell.x, maxCell.x + 1), maxCell.y + 2);
                entrySide = UrchinEnemy.EntrySide.Top;
                direction = -1f;
            }
            else
            {
                spawnCell = new Vector2Int(Random.Range(minCell.x, maxCell.x + 1), minCell.y - 2);
                entrySide = UrchinEnemy.EntrySide.Bottom;
                direction = 1f;
            }
        }

        Vector3 spawnPos = CellToWorldPosition(spawnCell);
        GameObject urchin = Instantiate(urchinPrefab, spawnPos, Quaternion.identity);

        UrchinEnemy urchinEnemy = urchin.GetComponent<UrchinEnemy>();
        urchinEnemy.urchinSpeed = urchinSpeed;
        urchinEnemy.entrySide = entrySide;
        urchinEnemy.spikeCount = numSpikes;

        SpriteRenderer spriteRend = urchin.GetComponentInChildren<SpriteRenderer>();
        if (spriteRend != null && entrySide == UrchinEnemy.EntrySide.Left)
            spriteRend.flipX = true;
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
