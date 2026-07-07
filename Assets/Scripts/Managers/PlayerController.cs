using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

[System.Serializable]
public class PlayerController
{
    public float2 pos;
    public float2 velocity;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float hitInvincibleDuration = 0.2f;
    private Transform playerTransform;
    private SpriteRenderer main;
    private SpriteRenderer spell;
    private Transform spellTransform;
    private float2 initialPos;
    private readonly float margin = 0.3f;
    private float2 xRange = new float2(0, 0);
    private float2 yRange = new float2(0, 0);
    private float hitInvincibleTimer = 0f;

    public bool invincible
    {
        get => dash > 0 || hitInvincibleTimer > 0f;
        private set { }
    }
    [SerializeField]
    private float dash = 0;
    private readonly float dashCooldown = 0.36f;

    public void Init(GameObject playerObj)
    {
        playerTransform = playerObj.transform;
        initialPos = new float2(playerTransform.position.x, playerTransform.position.y);
        pos = initialPos;
        main = playerObj.GetComponent<SpriteRenderer>();
        spellTransform = playerTransform.Find("Spell");
        if (spellTransform != null)
        {
            spell = spellTransform.GetComponent<SpriteRenderer>();
        }
        xRange = new float2(margin, 32 - margin);
        yRange = new float2(margin, 18 - margin);
    }

    // Update is called once per frame
    public void UpdatePos(float dt)
    {
        Move(dt);
        Dash(dt);
        UpdateHitState(dt);
        playerTransform.position = new Vector3(pos.x, pos.y, 0);

    }

    public bool TryHit()
    {
        if (invincible) return false;

        hitInvincibleTimer = hitInvincibleDuration;
        if (main != null)
        {
            //固定赤色
            main.color = new Color(1f, 0.35f, 0.35f, 1f);
        }

        return true;
    }

    public void ResetForStage()
    {
        pos = initialPos;
        velocity = float2.zero;
        hitInvincibleTimer = 0f;
        dash = -dashCooldown * 1.4f;
        if (main != null) main.color = GManager.Control.playerColor;
        if (spell != null) spell.color = Color.clear;
        if (playerTransform != null) playerTransform.position = new Vector3(pos.x, pos.y, 0f);
    }

    // marron keep コード(GManager / StageSelectManager)向け互換。開始位置(中央)への
    // リセットは raymee の ResetForStage と同義。
    public void ResetToCenter() => ResetForStage();

    private void Move(float dt)
    {
        float2 previousPos = pos;
        float2 inputVector = new float2(0, 0);
        if (GManager.Control.IManager.upPressed) inputVector.y += 1;
        if (GManager.Control.IManager.downPressed) inputVector.y -= 1;
        if (GManager.Control.IManager.leftPressed) inputVector.x -= 1;
        if (GManager.Control.IManager.rightPressed) inputVector.x += 1;

        if (math.length(inputVector) > 0)
        {
            inputVector = math.normalize(inputVector);
            pos += inputVector * dt * (dash > 0 ? dashSpeed : moveSpeed);
            pos.x = math.clamp(pos.x, xRange.x, xRange.y);
            pos.y = math.clamp(pos.y, yRange.x, yRange.y);
        }

        velocity = dt > 0f ? (pos - previousPos) / dt : new float2(0, 0);
    }

    private void Dash(float dt)
    {
        if (dash <= -dashCooldown * 1.4f && GManager.Control.IManager.buttonPressed)
        {
            dash = dashCooldown;
        }

        if (dash > 0)
        {
            float alpha = GetAlpha(dash);
            if (spell != null)
            {
                Color c = GManager.Control.playerColor;
                c.a = alpha;
                spell.color = c;
            }
        }
        else
        {
            if (spell != null) spell.color = new Color(0, 0, 0, 0);
        }

        if (spellTransform != null) spellTransform.rotation = Quaternion.Euler(0, 0, Time.time * 30);
        dash -= dt;
    }

    private void UpdateHitState(float dt)
    {
        if (hitInvincibleTimer <= 0f)
        {
            if (main != null) main.color = GManager.Control.playerColor;
            return;
        }

        hitInvincibleTimer = math.max(0f, hitInvincibleTimer - dt);
        if (hitInvincibleTimer <= 0f && main != null)
        {
            main.color = GManager.Control.playerColor;
        }
    }

    private float GetAlpha(float t)
    {
        if (t > dashCooldown - 0.1f) return (dashCooldown - t) / 0.1f;
        else if (t < 0.1f && t >= 0) return t / 0.1f;
        else if (t < 0) return 0;
        else return 1;
    }
}
