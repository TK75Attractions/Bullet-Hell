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
    private PlayerVisualController visual;
    private Transform spellTransform;
    private float2 initialPos;
    private readonly float margin = 0.3f;
    private float2 xRange = new float2(0, 0);
    private float2 yRange = new float2(0, 0);
    private float hitInvincibleTimer = 0f;
    private bool debugTransparent;
    private float debugTransparencyAlpha = 1f;
    private Color mainBaseColor = Color.white;
    private Color spellBaseColor = Color.clear;

    public bool invincible
    {
        get => dash > 0 || hitInvincibleTimer > 0f
            || (GManager.Control != null && GManager.Control.IsRaymeeDebugPlayerInvincible);
        private set { }
    }
    // カウンター判定用: ダッシュ中のみ true。被弾後の無敵時間(hitInvincibleTimer)は含めない
    // ため、ダッシュしていないのにカウンターが出る問題を防ぐ。
    public bool IsDashing => dash > 0;
    [SerializeField]
    private float dash = 0;
    private readonly float dashCooldown = 0.36f;

    public void Init(GameObject playerObj)
    {
        playerTransform = playerObj.transform;
        initialPos = new float2(playerTransform.position.x, playerTransform.position.y);
        pos = initialPos;
        main = playerObj.GetComponent<SpriteRenderer>();
        if (main != null) mainBaseColor = main.color;
        visual = playerObj.GetComponent<PlayerVisualController>();
        if (visual == null)
        {
            visual = playerObj.AddComponent<PlayerVisualController>();
        }
        visual.Initialize(
            main,
            GManager.Control != null ? GManager.Control.playerColor1 : PlayerPaletteDefaults.Color1,
            GManager.Control != null ? GManager.Control.playerColor2 : PlayerPaletteDefaults.Color2);
        spellTransform = playerTransform.Find("Spell");
        if (spellTransform != null)
        {
            spell = spellTransform.GetComponent<SpriteRenderer>();
            if (spell != null) spellBaseColor = spell.color;
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
        visual?.UpdateVisual(
            dt,
            velocity.x,
            GManager.Control != null ? GManager.Control.playerColor1 : PlayerPaletteDefaults.Color1,
            GManager.Control != null ? GManager.Control.playerColor2 : PlayerPaletteDefaults.Color2);
        playerTransform.position = new Vector3(pos.x, pos.y, 0);

    }

    public bool TryHit()
    {
        if (invincible) return false;

        hitInvincibleTimer = hitInvincibleDuration;
        //固定赤色
        SetMainColor(new Color(1f, 0.35f, 0.35f, 1f));

        return true;
    }

    public void ResetForStage()
    {
        pos = initialPos;
        velocity = float2.zero;
        hitInvincibleTimer = 0f;
        dash = -dashCooldown * 1.4f;
        SetMainColor(Color.white);
        visual?.ResetAnimation();
        SetSpellColor(Color.clear);
        if (playerTransform != null) playerTransform.position = new Vector3(pos.x, pos.y, 0f);
    }

    // marron keep コード(GManager / StageSelectManager)向け互換。開始位置(中央)への
    // リセットは raymee の ResetForStage と同義。
    public void ResetToCenter() => ResetForStage();

    public void SetVisualColors(Color color1, Color color2)
    {
        visual?.SetColors(color1, color2);
    }

    /// <summary>
    /// Raymee デバッグ用の透過表示を切り替える。実際の描画色は基準色として保持するため、
    /// 被弾演出やダッシュ演出による色・アルファの変化を壊さない。
    /// </summary>
    public void SetDebugTransparency(bool enabled, float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        if (debugTransparent == enabled && Mathf.Approximately(debugTransparencyAlpha, clampedAlpha)) return;

        debugTransparent = enabled;
        debugTransparencyAlpha = clampedAlpha;
        ApplyDisplayColors();
    }

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
                SetSpellColor(c);
            }
        }
        else
        {
            SetSpellColor(Color.clear);
        }

        if (spellTransform != null) spellTransform.rotation = Quaternion.Euler(0, 0, Time.time * 30);
        dash -= dt;
    }

    private void UpdateHitState(float dt)
    {
        if (hitInvincibleTimer <= 0f)
        {
            SetMainColor(Color.white);
            return;
        }

        hitInvincibleTimer = math.max(0f, hitInvincibleTimer - dt);
        if (hitInvincibleTimer <= 0f)
        {
            SetMainColor(Color.white);
        }
    }

    private void SetMainColor(Color color)
    {
        mainBaseColor = color;
        if (main != null) main.color = ApplyDebugTransparency(color);
    }

    private void SetSpellColor(Color color)
    {
        spellBaseColor = color;
        if (spell != null) spell.color = ApplyDebugTransparency(color);
    }

    private void ApplyDisplayColors()
    {
        if (main != null) main.color = ApplyDebugTransparency(mainBaseColor);
        if (spell != null) spell.color = ApplyDebugTransparency(spellBaseColor);
    }

    private Color ApplyDebugTransparency(Color color)
    {
        color.a *= debugTransparent ? debugTransparencyAlpha : 1f;
        return color;
    }

    private float GetAlpha(float t)
    {
        if (t > dashCooldown - 0.1f) return (dashCooldown - t) / 0.1f;
        else if (t < 0.1f && t >= 0) return t / 0.1f;
        else if (t < 0) return 0;
        else return 1;
    }
}
