using UnityEngine;

public class Main : MonoBehaviour
{
    [Header("Shark")]
    [SerializeField] GameObject sharkPrefab; 
    public float sharkSpawnPerSecond = 0.5f;
    private float nextSpawnTime = 0f;
    public float sharkSpeed = 5f;

    [Header("Bounds")]
    [SerializeField] Vector2Int minCell = new Vector2Int(-8, -4);
    [SerializeField] Vector2Int maxCell = new Vector2Int(7, 3);


    // Update is called once per frame
    void Update()
    {
        if (Time.time >= nextSpawnTime)
        {
            SpawnShark();
            nextSpawnTime = Time.time + (1f / sharkSpawnPerSecond);
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
