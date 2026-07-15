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
    // 被弾時に飛ばす軽いパーティクル(2026-07-13 指摘)。既存の赤フラッシュに重ねる
    // 少量バースト。色は被弾ごとに playerColor(パレット準拠)で発射する。
    private ParticleSystem hitBurst;
    private const int HitBurstCount = 14;

    // --- 2P 対応(その1) ---
    // どちらのプレイヤーか。0=P1(既定・従来どおり P1 入力/色)、1=P2。
    // new PlayerController() 後、Init 前に GManager が設定する。
    public int playerIndex = 0;
    // 主人公アートは色付き(探偵)。従来の playerColor 乗算では色が濁るため、白ベースで
    // スプライト本来の色をそのまま出す(P2 の淡色化はシート側で焼込済)。被弾/リセットでは
    // SetMainColor が基準色を書き換える(Raymee のデバッグ透過と両立)ため可変インスタンスにする。
    private Color mainBaseColor = Color.white;
    // ダッシュ演出(Spell)の基準色。SetSpellColor/デバッグ透過が参照する(main 統合)。
    private Color spellBaseColor = Color.clear;
    // Raymee 専用デバッグの透過表示(main 統合)。基準色は保持したまま描画αだけ落とす。
    private bool debugTransparent;
    private float debugTransparencyAlpha = 1f;
    // 被弾パーティクル/ダッシュ光のトーン色。P1=従来 playerColor 準拠(=1P 不変)、
    // P2=寒色系。Init で playerIndex に応じて確定する。
    private Color toneColor = new Color(1f, 1f, 0.6f, 1f);
    // 方向別アニメ(左/前後/右・各8フレーム)。Resources/PlayerSprites から読む。
    // P1 は原色シート、P2 は彩度落としシート(p2_*)。
    private Sprite[] framesLeft, framesFront, framesRight;
    private Sprite[] lastSet;   // アイドル時に向きを保つため直近に使ったシートを覚える
    private int animFrame = 0;
    private float animTimer = 0f;
    private const float AnimFps = 12f;
    // 新主人公(探偵)は 128px セル内で実キャラ約72px=0.56units と旧 Rumia(約1unit)より
    // 小さいので、旧主人公と同等の見え方(約1unit)へ拡大する。当たり判定は点(pos)基準の
    // ため見た目のみに影響。実フレームで 1.85 が旧サイズ相当と確認(2P その1)。
    private const float PlayerVisualScale = 1.85f;
    // ダッシュエフェクト(Spell 子)の縮小率。親スケール(1.85)とは独立の子スケール乗数。
    private const float DashEffectScale = 0.6f;

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
            if (spell != null) spellBaseColor = spell.color;   // main: 基準色を保持(デバッグ透過用)
            // ダッシュエフェクト(Spell オーラ)を縮小して馴染ませる(2026-07-14 指摘「ダッシュ時のエフェクトが大きい」)。
            // 親(player)の 1.85 倍とは独立の子スケール。0.6 で現状の 0.6 倍。
            spellTransform.localScale = Vector3.one * DashEffectScale;
        }
        xRange = new float2(margin, 32 - margin);
        yRange = new float2(margin, 18 - margin);

        // 方向別アニメと被弾トーン色を playerIndex に応じて用意する。
        string prefix = playerIndex == 1 ? "p2" : "p1";
        framesLeft = LoadFrames(prefix + "_left");
        framesFront = LoadFrames(prefix + "_front");
        framesRight = LoadFrames(prefix + "_right");
        lastSet = framesFront;
        if (playerTransform != null) playerTransform.localScale = Vector3.one * PlayerVisualScale;
        // P1 は従来の playerColor をそのまま採用し 1P の演出色を不変に保つ。
        // P2 は視認しやすい寒色トーン(淡いシアン)にして P1 と区別する。
        toneColor = playerIndex == 1
            ? new Color(0.45f, 0.85f, 1f, 1f)
            : (GManager.Control != null ? GManager.Control.playerColor : new Color(1f, 1f, 0.6f, 1f));
        if (main != null) main.color = mainBaseColor;

        SetupHitBurst();
    }

    // Resources/PlayerSprites/{name}.png のスライス済みスプライト8枚を名前順で読む。
    // Resources.LoadAll の返却順は保証されないため CompareOrdinal で _0.._7 に整列する。
    private static Sprite[] LoadFrames(string name)
    {
        Sprite[] arr = Resources.LoadAll<Sprite>("PlayerSprites/" + name);
        if (arr == null || arr.Length == 0)
        {
            Debug.LogWarning($"[PlayerController] スプライトが見つかりません: PlayerSprites/{name}");
            return arr ?? new Sprite[0];
        }
        System.Array.Sort(arr, (a, b) => string.CompareOrdinal(a.name, b.name));
        return arr;
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
        // 毎フレームのスプライト駆動は feature の方向別 8 フレームアニメ(P1/P2 対応)を採用。
        // main の visual.UpdateVisual(palette-swap)は同じ SpriteRenderer を奪い合うため
        // per-frame では呼ばない(色は SetVisualColors から visual へ反映)。
        UpdateAnimation(dt);
        playerTransform.position = new Vector3(pos.x, pos.y, 0);

    }

    // 入力方向で左/前後/右シートを選び、移動中は 8 フレームを AnimFps で循環。
    // 静止時も前後(front)シートを循環させ続けて「ひらひら」を常時見せる。向きは正面(front)固定のまま
    // (2026-07-14「静止時はデフォルト正面に」の向き固定は保つ・フレーム循環だけ復活)。
    // 横入力を優先し、縦のみは前後シート。
    private void UpdateAnimation(float dt)
    {
        if (main == null) return;
        if (lastSet == null || lastSet.Length == 0) lastSet = framesFront;

        bool up = ReadUp(), down = ReadDown(), left = ReadLeft(), right = ReadRight();
        bool moving = up || down || left || right;

        Sprite[] set = lastSet;
        if (left && !right) set = framesLeft;
        else if (right && !left) set = framesRight;
        else if (up || down) set = framesFront;
        if (set == null || set.Length == 0) return;
        lastSet = set;

        if (moving)
        {
            animTimer += dt * AnimFps;
            animFrame = ((int)animTimer) % set.Length;
        }
        else
        {
            // 静止時も前後(front)シートへ揃えつつ循環を続ける。横向きのまま止まらず、正面で「ひらひら」。
            if (framesFront != null && framesFront.Length > 0)
            {
                set = framesFront;
                lastSet = set;
            }
            animTimer += dt * AnimFps;
            animFrame = ((int)animTimer) % set.Length;
        }
        main.sprite = set[math.clamp(animFrame, 0, set.Length - 1)];
    }

    // 入力の読み口。playerIndex に応じて P1(既存フィールド)か P2(p2*)を返す。
    private InputManager IM => GManager.Control.IManager;
    private bool ReadUp() => playerIndex == 1 ? IM.p2Up : IM.upPressed;
    private bool ReadDown() => playerIndex == 1 ? IM.p2Down : IM.downPressed;
    private bool ReadLeft() => playerIndex == 1 ? IM.p2Left : IM.leftPressed;
    private bool ReadRight() => playerIndex == 1 ? IM.p2Right : IM.rightPressed;
    private bool ReadButton() => playerIndex == 1 ? IM.p2ButtonPressed : IM.buttonPressed;

    public bool TryHit()
    {
        if (invincible) return false;

        hitInvincibleTimer = hitInvincibleDuration;
        //固定赤色
        SetMainColor(new Color(1f, 0.35f, 0.35f, 1f));

        // 被弾パーティクルを少量発射(色はパレット準拠の playerColor)。既存の赤
        // フラッシュに重ねる補助演出。無敵中(dash/i-frame)は上で return するので出ない。
        if (hitBurst != null)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            Color pc = toneColor;
            pc.a = 1f;
            ep.startColor = pc;
            hitBurst.Emit(ep, HitBurstCount);
        }

        return true;
    }

    public void ResetForStage()
    {
        ResetForStageAt(initialPos);
    }

    // 2P の左右配置を切り替えるときに、プレイヤーごとの開始位置へリセットする。
    // 1P の ResetForStage は従来どおり Init 時の initialPos を使う。
    public void ResetForStageAt(float2 startPosition)
    {
        pos = startPosition;
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
        if (ReadUp()) inputVector.y += 1;
        if (ReadDown()) inputVector.y -= 1;
        if (ReadLeft()) inputVector.x -= 1;
        if (ReadRight()) inputVector.x += 1;

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
        if (dash <= -dashCooldown * 1.4f && ReadButton())
        {
            dash = dashCooldown;
        }

        if (dash > 0)
        {
            float alpha = GetAlpha(dash);
            if (spell != null)
            {
                Color c = toneColor;
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
