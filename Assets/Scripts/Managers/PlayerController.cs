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
    // 被弾時に飛ばす軽いパーティクル(2026-07-13 指摘)。既存の赤フラッシュに重ねる
    // 少量バースト。色は被弾ごとに playerColor(パレット準拠)で発射する。
    private ParticleSystem hitBurst;
    private const int HitBurstCount = 14;

    public bool invincible
    {
        get => dash > 0 || hitInvincibleTimer > 0f;
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
        spellTransform = playerTransform.Find("Spell");
        if (spellTransform != null)
        {
            spell = spellTransform.GetComponent<SpriteRenderer>();
        }
        xRange = new float2(margin, 32 - margin);
        yRange = new float2(margin, 18 - margin);

        SetupHitBurst();
    }

    // 被弾パーティクルの生成(プレイヤー配下・World シミュレーションで、被弾位置に
    // 散って飛ぶ)。URP でも確実に描ける Sprites/Default + 手続きソフトドットを使う。
    // 連続発生は無し(rateOverTime=0)、被弾時に Emit で少量だけ出す=過剰にならない・
    // 常時負荷ゼロ。既存の赤フラッシュ演出に重ねる補助エフェクト。
    private void SetupHitBurst()
    {
        if (playerTransform == null) return;

        GameObject go = new GameObject("HitBurst");
        go.transform.SetParent(playerTransform, false);
        go.transform.localPosition = Vector3.zero;

        hitBurst = go.AddComponent<ParticleSystem>();
        hitBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule psMain = hitBurst.main;
        psMain.duration = 0.6f;
        psMain.loop = false;
        psMain.playOnAwake = false;
        psMain.startLifetime = new ParticleSystem.MinMaxCurve(0.26f, 0.5f);
        psMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
        psMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        psMain.gravityModifier = 0.55f;
        psMain.simulationSpace = ParticleSystemSimulationSpace.World;
        psMain.maxParticles = 48;

        ParticleSystem.EmissionModule emission = hitBurst.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;   // 常時放出なし。被弾時に Emit で出す

        ParticleSystem.ShapeModule shape = hitBurst.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.16f;
        shape.radiusThickness = 1f;

        // 寿命でフェードアウト(alpha 1→0)。
        ParticleSystem.ColorOverLifetimeModule col = hitBurst.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        // 寿命で少し縮む。
        ParticleSystem.SizeOverLifetimeModule size = hitBurst.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.25f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = CreateSoftDotTexture();
        psr.material = mat;
        psr.sortingOrder = 100;   // プレイヤー/弾より前面に

        hitBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // 中心が白・外周に向けて透明になる 32x32 のソフトドット。
    private static Texture2D CreateSoftDotTexture()
    {
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;   // 中心をやや強く、外周を柔らかく
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
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

        // 被弾パーティクルを少量発射(色はパレット準拠の playerColor)。既存の赤
        // フラッシュに重ねる補助演出。無敵中(dash/i-frame)は上で return するので出ない。
        if (hitBurst != null)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            Color pc = GManager.Control != null ? GManager.Control.playerColor : Color.white;
            pc.a = 1f;
            ep.startColor = pc;
            hitBurst.Emit(ep, HitBurstCount);
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
