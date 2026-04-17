using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

[System.Serializable]
public class PlayerController
{
    public float2 pos;
    public int HP = 5;
    private float invincibilityTime = 0f;
    private const float invincibilityDuration = 1f; // 1 second of invincibility after being hit

    private float dodgeIncivibilityTime = 0f;
    private const float dodgeIncivibilityDuration = 0.5f; // 0.5 seconds of invincibility after dodging
    private float dodgeCooldownTime = 0f;
    private const float dodgeCooldownDuration = 0.8f; // 0.8 seconds cooldown for dodge

    private float2 velocity;
    private float angle = 0f;
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
    public void UpdatePos(float dt, bool buttonPressed)
    {
        Move(dt);
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
            velocity = inputVector * moveSpeed;
            angle = math.atan2(inputVector.y, inputVector.x) * Mathf.Rad2Deg;
            playerTransform.rotation = Quaternion.Euler(0, 0, angle - 90);
        }
        else
        {
            velocity = float2.zero;
        }

        pos += velocity * dt;
    }

    private void HandleInvincibility(float dt)
    {
        if (invincibilityTime > 0)
        {
            invincibilityTime -= dt;

            int i = (int)(invincibilityTime * 10) % 2;

            // Flash the player sprite to indicate invincibility
            SR.color = new Color(1, 1, 1, i == 0 ? 0.5f : 1f);
        }

        if (dodgeIncivibilityTime > 0)
        {
            dodgeIncivibilityTime -= dt;

            int i = (int)(dodgeIncivibilityTime * 10) % 2;

            // Flash the player sprite to indicate invincibility
            SR.color = new Color(1, 1, 1, i == 0 ? 0.5f : 1f);
        }
    }
    #region Collision

    #endregion
}
