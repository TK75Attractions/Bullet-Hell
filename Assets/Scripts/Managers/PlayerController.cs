using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

[System.Serializable]
public class PlayerController
{
    public float2 pos;
    [SerializeField] private float moveSpeed = 5f;
    private Transform playerTransform;
    [SerializeField] private SpriteRenderer SR;

    public void Init(GameObject playerObj)
    {
        playerTransform = playerObj.transform;
        SR = playerObj.GetComponent<SpriteRenderer>();
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    public void UpdatePos(float dt)
    {
        Move(dt);
        playerTransform.position = new Vector3(pos.x, pos.y, 0);

    }

    private void Move(float dt)
    {
        float2 inputVector = new float2(0, 0);
        if (GManager.Control.IManager.up) inputVector.y += 1;
        if (GManager.Control.IManager.down) inputVector.y -= 1;
        if (GManager.Control.IManager.left) inputVector.x -= 1;
        if (GManager.Control.IManager.right) inputVector.x += 1;

        if (math.length(inputVector) > 0)
        {
            inputVector = math.normalize(inputVector);
            pos += inputVector * moveSpeed * dt;
        }
    }

    #region Collision
    public void UpShot()
    {
        NativeArray<BulletData> bullets = GetListOfUpBullets(pos);
        GManager.Control.QOrder.AddPlayerBullets(bullets);
        bullets.Dispose();
    }
    
    private NativeArray<BulletData> GetListOfUpBullets(float2 _pos)
    {
        NativeArray<BulletData> bullets = new NativeArray<BulletData>(6, Allocator.Temp);
        BulletData b0 = CreatePlayerBullet(_pos + new float2(0.2f, 0.5f), new float4(0, 0, 0, 0));
        BulletData b1 = CreatePlayerBullet(_pos + new float2(0.3f, 0.4f), new float4(0, 0, 0, 0));
        BulletData b2 = CreatePlayerBullet(_pos + new float2(0.4f, 0.3f), new float4(-0.4f, 0, 0, 0));
        BulletData b3 = CreatePlayerBullet(_pos + new float2(-0.2f, 0.5f), new float4(0, 0, 0, 0));
        BulletData b4 = CreatePlayerBullet(_pos + new float2(-0.3f, 0.4f), new float4(0, 0, 0, 0));
        BulletData b5 = CreatePlayerBullet(_pos + new float2(-0.4f, 0.3f), new float4(0.4f, 0, 0, 0));

        bullets[0] = b0;
        bullets[1] = b1;
        bullets[2] = b2;
        bullets[3] = b3;
        bullets[4] = b4;
        bullets[5] = b5;
        return bullets;
    }

    private BulletData CreatePlayerBullet(float2 position, float4 polynomial)
    {
        return new BulletData(
            position,
            new float2(0, 0),
            3f,
            0f,
            0f,
            new float2(1, math.PI / 2),
            0f,
            0f,
            0f,
            polynomial,
            2,
            1f,
            new float4(255, 255, 255, 255)
        );
    }

    public void DownShot()
    {

    }

    #endregion
}
