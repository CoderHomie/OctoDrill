using UnityEngine;

public class SharkEnemy : MonoBehaviour
{
    [Header("Shark Attributes")]
    public float sharkSpeed = 5f;
    

    public Vector3 pos
    {
        get{return this.transform.position;}
        set{this.transform.position = value;}
    }
    // Update is called once per frame
    void Update()
    {
        Move();
        CheckBounds();
    }

    public virtual void Move()
    {
        Vector3 tempPos = pos;
        tempPos.x += sharkSpeed * Time.deltaTime;
        pos = tempPos;
    }

    void CheckBounds()
    {
        // Destroy if far off-screen (adjust threshold as needed)
        if (transform.position.x < -15f || transform.position.x > 20f)
        {
            Destroy(gameObject);
        }
    }
}
