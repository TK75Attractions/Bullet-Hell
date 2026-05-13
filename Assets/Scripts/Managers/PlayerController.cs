using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

[System.Serializable]
public class PlayerController
{
    public float2 pos;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float dashSpeed = 20f;
    private Transform playerTransform;
    private SpriteRenderer SR;
    public bool invincible
    {
        get => dash > 0;
        private set { }
    }
    private float dash = 0;
    private readonly float dashCooldown = 0.36f;


    public void Init(GameObject playerObj)
    {
        playerTransform = playerObj.transform;
        SR = playerObj.GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    public void UpdatePos(float dt)
    {
        Move(dt);
        Dash(dt);
        playerTransform.position = new Vector3(pos.x, pos.y, 0);

    }

    private void Move(float dt)
    {
        float2 inputVector = new float2(0, 0);
        if (GManager.Control.IManager.upPressed) inputVector.y += 1;
        if (GManager.Control.IManager.downPressed) inputVector.y -= 1;
        if (GManager.Control.IManager.leftPressed) inputVector.x -= 1;
        if (GManager.Control.IManager.rightPressed) inputVector.x += 1;

        if (math.length(inputVector) > 0)
        {
            inputVector = math.normalize(inputVector);
            pos += inputVector * dt * (dash > 0 ? dashSpeed : moveSpeed);
        }
    }

    private void Dash(float dt)
    {
        if (dash <= -dashCooldown * 1.4f && GManager.Control.IManager.buttonPressed)
        {
            dash = dashCooldown;
        }

        dash -= dt;
    }

    #region Collision
    #endregion
}
