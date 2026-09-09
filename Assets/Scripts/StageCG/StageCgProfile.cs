using System;
using UnityEngine;

/// <summary>
/// 背景 CG の形態変化の種類。ステージごとに絵づくりが違うので、共通の
/// 「p1_* / p2_* の入れ替え」に加えて何を動かすかをここで選ぶ。
/// </summary>
public enum StageCgPhaseKind
{
    /// <summary>形態変化なし。</summary>
    None = 0,
    /// <summary>石工: ゴーレム降臨（揺れ・粉・ランタン消灯・コアの赤い光・割れ目の発光）。</summary>
    StoneGolem = 1,
    /// <summary>艦長: 錨と鎖が落ちる（帆と信号旗が張られ、ランタンの光輪が灯る）。</summary>
    CaptainAnchor = 2,
    /// <summary>浮浪者: 幽霊のリング（人魂が漂い、残り火が青白くなり、霧が流れる）。</summary>
    VagrantWisp = 3,
}

/// <summary>
/// ボス 1 体ぶんの「時刻 → CG 空間の奥行き z」。石工 v34 で老人が岩棚の奥へ回り込み、
/// ゴーレムの後ろから飛び乗るために足した。キーの間は線形補間する。
/// </summary>
[Serializable]
public class StageCgBossDepthKey
{
    [Tooltip("ステージ秒。")]
    public float time;
    [Tooltip("その時刻の奥行き z（プロファイルの bossDepth と同じ意味）。")]
    public float depth = 5.5f;
}

/// <summary>
/// 1 体のボス（bossSpawner の bossId）に対する奥行きの上書き。
/// bossId が一致しないボスと、トラックを持たないステージは bossDepth のまま。
/// </summary>
[Serializable]
public class StageCgBossDepthTrack
{
    [Tooltip("stone.json の bossSpawner.bossId。")]
    public string bossId = "";
    public StageCgBossDepthKey[] keys = new StageCgBossDepthKey[0];
}

/// <summary>
/// 1 体のボスだけ明度を変える上書き（石工 v35 #4「老人をもっと手前に・少し明るく」）。
/// 一覧に無いボスと、上書きを持たないステージは bossBrightness のまま。
/// </summary>
[Serializable]
public class StageCgBossBrightnessOverride
{
    [Tooltip("stone.json の bossSpawner.bossId。")]
    public string bossId = "";
    [Range(0f, 2f)] public float brightness = 0.5f;
}

/// <summary>
/// ステージ 1 本ぶんの背景 CG 設定。<see cref="StageCgController"/> が
/// ステージ id で 1 つ選び、その値だけを使って描く。
///
/// 既定値は石工（2026-09-07〜08 に実測で決めた値）。艦長・浮浪者はシーン側で上書きする。
/// カメラの通常姿勢・非対称フラスタムは 3 ステージ共通なのでコントローラ側に置いた。
/// </summary>
[Serializable]
public class StageCgProfile
{
    [Header("対象ステージ")]
    [Tooltip("この stageDirectoryName のときだけこのプロファイルを使う。")]
    public string stageDirectory = "stone";
    [Tooltip("stageDirectoryName が空のときの保険（stageName 一致）。")]
    public string stageNameFallback = "石工";
    [Tooltip("このステージの CG 本体（レイヤー StageCG）。プロファイルが選ばれた間だけ有効になる。")]
    public GameObject sceneRoot;

    [Header("明るさ")]
    [Tooltip("表示板の露出。Astra のレンダーよりかなり暗くする。")]
    [Range(0f, 2f)] public float exposure = 0.35f;
    [Tooltip("フィールド中央（弾が飛ぶ帯）を落として弾の視認性を上げる量。")]
    [Range(0f, 1f)] public float centerDarken = 0.55f;
    [Tooltip("ボスの明度。CG の露出・中央減光とは独立に掛かる。")]
    [Range(0f, 2f)] public float bossBrightness = 0.5f;
    [Tooltip("ボスを置く奥行き。舞台の手前縁と同じ z。")]
    public float bossDepth = 5.5f;
    [Tooltip("ボス個体ごとに奥行きを時間で上書きする（石工の老人が棚の奥へ回り込む用）。空なら bossDepth のまま。")]
    public StageCgBossDepthTrack[] bossDepthTracks = new StageCgBossDepthTrack[0];
    [Tooltip("ボス個体ごとの明度の上書き。空なら bossBrightness のまま。")]
    public StageCgBossBrightnessOverride[] bossBrightnessOverrides = new StageCgBossBrightnessOverride[0];

    [Header("ライティング（Blender 側の数値をリニアで再現）")]
    public Vector3 sunFrom = new Vector3(-30f, 55f, -12f);
    public Vector3 sunTo = new Vector3(16f, 2f, 12f);
    public Vector3 sunColorLinear = new Vector3(0.64f, 0.59f, 1f);
    [Tooltip("Blender の sun energy をランバート応答へ換算した値（energy/π）。")]
    public float sunIntensity = 0.5252f;
    [Tooltip("world background * strength。")]
    public Vector3 ambientLinear = new Vector3(0.0345f, 0.0294f, 0.054f);

    [Header("見上げカメラ（各 v*_camera.json / notes の実値）")]
    public Vector3 lookupPosition = new Vector3(16f, 20f, -28f);
    public Vector3 lookupTarget = new Vector3(16f, 58f, 72f);
    [Tooltip("見上げのレンズ mm（sensor 36mm・水平フィット・対称フラスタム）。")]
    public float lookupLensMm = 32f;

    [Header("導入（ステージ秒）")]
    [Tooltip("黒 → 空のフェード開始 / 終了。")]
    public float blackFadeStart = 0.56f;
    public float blackFadeEnd = 1.30f;
    [Tooltip("見上げ姿勢を保つ終わり。ここから通常姿勢へ動き出す。")]
    public float cameraHoldEnd = 1.06f;
    [Tooltip("通常姿勢へ着地する時刻。中点で速度最大になる smoothstep。")]
    public float cameraSettleEnd = 4.56f;

    [Tooltip("自機を下から登場させるか。false なら従来どおり最初から操作できる。")]
    public bool playerIntroEnabled = true;
    public float playerRiseStart = 4.071f;
    [Tooltip("自機が初期位置に着いて操作可能になる時刻。")]
    public float playerRiseEnd = 4.726f;
    [Tooltip("登場前に自機を置いておく画面外の y。")]
    public float playerEnterY = -3f;
    public float playerGoalX = 16f;
    public float playerGoalY = 3f;

    [Header("拍連動")]
    [Tooltip("1 拍の長さ（秒）。石工 BPM144 = 0.4166667 / 艦長 BPM110 = 0.5454545 / 浮浪者 BPM199.5 = 0.3007519。")]
    public float beatSec = 60f / 144f;
    [Tooltip("拍格子の原点（ステージ秒）。曲の頭がステージ時計の 0 でないステージ（艦長は delayTime -2.76 なので 2.76）で使う。")]
    public float beatOffsetSec = 0f;
    [Tooltip("拍頭で発光グループ 1・2 を何割増やすか。")]
    [Range(0f, 1f)] public float beatPulse = 0.2f;
    [Tooltip("拍頭の増分が戻るまでの時間。")]
    public float beatDecaySec = 0.15f;

    [Header("色調整（表示板シェーダ・第 6 便 C）")]
    [Tooltip("背景 CG の色相を回す角度（度）。0 で無変換。ボスの画素には掛からない。")]
    [Range(-180f, 180f)] public float hueShiftDeg = 0f;
    [Tooltip("背景 CG の彩度。1 でそのまま。")]
    [Range(0f, 2f)] public float saturation = 1f;
    [Tooltip("被せる色。輝度を保ったまま tintAmount の割合だけこの色相へ寄せる。")]
    public Color tintColor = Color.white;
    [Range(0f, 1f)] public float tintAmount = 0f;

    [Header("形態変化")]
    public StageCgPhaseKind phase = StageCgPhaseKind.None;
    [Tooltip("フェーズが切り替わるステージ秒。石工 60.028（v34 で 72.94 から移動）/ 艦長 35.2 / 浮浪者 45.714。")]
    public float phaseTime = 60.028f;
    [Tooltip("p1_* → p2_* のクロスフェードにかける時間。")]
    public float phaseCrossfadeSec = 0.5f;
    [Tooltip("第 2 フェーズで隠す既存オブジェクトの名前（前方一致）。")]
    public string[] hideInPhase2 = new string[0];

    [Header("形態変化の見せ方（第 6 便 A）")]
    [Tooltip("切替の瞬間の控えめなフラッシュの長さ。0 で無し。")]
    public float phaseFlashSec = 0.12f;
    [Tooltip("フラッシュの強さ（画面の端と上部だけ +この割合）。")]
    [Range(0f, 1f)] public float phaseFlashAmount = 0.15f;
    [Tooltip("切替後の色調・露出へ移るのにかける時間。")]
    public float phaseColorBlendSec = 0.5f;
    [Range(-180f, 180f)] public float phase2HueShiftDeg = 0f;
    [Range(0f, 2f)] public float phase2Saturation = 1f;
    public Color phase2TintColor = Color.white;
    [Range(0f, 1f)] public float phase2TintAmount = 0f;
    [Tooltip("切替後の露出倍率（石工の旧 lateExposureScale をここへ一般化）。")]
    [Range(0f, 2f)] public float phase2ExposureScale = 1f;
    [Tooltip("切替後の発光グループ 1（ランタン・回路・人魂・紋様）の倍率。")]
    [Range(0f, 4f)] public float phase2Group1Scale = 1f;
    [Tooltip("切替後の発光グループ 2（街・港の灯り）の倍率。")]
    [Range(0f, 4f)] public float phase2Group2Scale = 1f;
    [Tooltip("p2_* を 1 つずつ順に灯すときの間隔（浮浪者の人魂）。0 なら一斉。")]
    public float phase2SequentialSec = 0f;
    [Tooltip("順に灯す対象の名前（前方一致）。空なら p2_* 全部。")]
    public string phase2SequentialPrefix = "";
    [Tooltip("p2_* が上端を軸に縦へ伸びて「降りてくる」時間（艦長の帆・信号旗）。0 なら無し。")]
    public float phase2DropSec = 0f;
    [Tooltip("降りてくる対象の名前（前方一致）。")]
    public string[] phase2DropPrefixes = new string[0];

    [Header("石工の降臨演出（phase = StoneGolem のときだけ）")]
    [Tooltip("降下にかかる時間（降下開始 = phaseTime - この値）。")]
    public float descendSec = 0.833333f;
    public float shakeAmplitude = 0.6f;
    public float shakeDuration = 0.34f;
    public float shakeFrequency = 18f;
    [Tooltip("着地でランタンを消しておく時間。")]
    public float lanternBlackoutSec = 0.3f;
    [Tooltip("落ちる粉に使うマテリアル。未設定なら粉を出さない。")]
    public Material dustMaterial;
    public int dustCount = 60;
    public float dustLifeSec = 1.5f;
    [Tooltip("コアの赤い点光源の位置。")]
    public Vector3 coreLightPosition = new Vector3(16f, 13.1f, 5.7f);
    public Vector3 coreColorLinear = new Vector3(1f, 0.25f, 0.2f);
    public float coreRadius = 10f;
    public float coreIntensity = 1.7f;
    [Range(0f, 1f)] public float corePulse = 0.25f;
    [Range(0f, 1f)] public float lateLanternScale = 0.6f;
    [Range(0f, 1f)] public float lateCityScale = 0.6667f;
    [Tooltip("後半の割れ目の発光倍率（コアの明滅に掛ける）。")]
    [Range(0f, 4f)] public float lateCrackScale = 1f;
    [Tooltip("着地の粉の大きさ倍率と、発生を散らす秒数。")]
    public float dustSizeScale = 1f;
    public float dustSpawnSpreadSec = 0.5f;
    public Vector3 lateSkyTint = new Vector3(1.06f, 0.94f, 1.02f);
    [Range(0f, 1f)] public float lateExposureScale = 0.98f;

    [Header("浮浪者の人魂・霧（phase = VagrantWisp のときだけ）")]
    [Tooltip("p2_wisp_* が上下に揺れる振幅（ユニット）。")]
    public float wispAmplitude = 0.15f;
    [Tooltip("上下運動の周期（個体ごとに min..max を割り当て、位相もずらす）。")]
    public float wispPeriodMin = 2f;
    public float wispPeriodMax = 3f;
    [Tooltip("p2_fog_* が横へ流れる速さ（ユニット/秒）と振れ幅。")]
    public float fogDriftSpeed = 0.06f;
    public float fogDriftRange = 0.9f;
    [Tooltip("p1_dust_* が横へ流れる速さ。")]
    public float dustDriftSpeed = 0.10f;
    public float dustDriftRange = 1.2f;

    [Header("終端の暗転（石工 v34・指示書 #22 #23）")]
    [Tooltip("true のとき、白転（PixelTransition のモザイク）ではなく黒フェードでリザルトへ移る。")]
    public bool useBlackEnding = true;
    [Tooltip("true なら暗転の時刻を endTime から自動で決める（背景 = endTime-1.0 / 全体 = endTime-0.4）。"
        + "false なら下の cgBlackoutTime / screenBlackoutTime をそのまま使う（石工の指示時刻）。")]
    public bool endingTimesFromEndTime = true;
    [Tooltip("#22 背景 CG だけを黒へ落とし始めるステージ秒。ボスはそのまま見え続ける。")]
    public float cgBlackoutTime = 141.745f;
    public float cgBlackoutSec = 0.6f;
    [Tooltip("#23 画面全体を黒へ落とし始めるステージ秒（ボスも弾も含む）。")]
    public float screenBlackoutTime = 146.72f;
    public float screenBlackoutSec = 0.4f;

    /// <summary>背景の暗転が始まるステージ秒。endingTimesFromEndTime なら endTime から決める。</summary>
    public float CgBlackoutTimeFor(float endTime)
        => endingTimesFromEndTime && endTime > 0f ? endTime - 1f : cgBlackoutTime;

    /// <summary>画面全体の暗転が始まるステージ秒。endTime で真っ黒になるよう 0.4 秒前から。</summary>
    public float ScreenBlackoutTimeFor(float endTime)
        => endingTimesFromEndTime && endTime > 0f ? endTime - screenBlackoutSec : screenBlackoutTime;

    /// <summary>背景 CG だけに掛ける減光 1..0（1=そのまま / 0=真っ黒）。</summary>
    public float CgBlackout(float stageTime, float endTime)
    {
        if (!useBlackEnding) return 1f;
        float u = Mathf.Clamp01((stageTime - CgBlackoutTimeFor(endTime)) / Mathf.Max(1e-4f, cgBlackoutSec));
        return 1f - u * u * (3f - 2f * u);
    }

    /// <summary>画面全体を覆う黒の濃さ 0..1。</summary>
    public float ScreenBlackout(float stageTime, float endTime)
    {
        if (!useBlackEnding) return 0f;
        float u = Mathf.Clamp01((stageTime - ScreenBlackoutTimeFor(endTime)) / Mathf.Max(1e-4f, screenBlackoutSec));
        return u * u * (3f - 2f * u);
    }

    /// <summary>形態変化の切替からの色調ブレンド 0..1（0=切替前 / 1=切替後）。</summary>
    public float PhaseColorBlend(float stageTime)
    {
        if (phase == StageCgPhaseKind.None) return 0f;
        float u = Mathf.Clamp01((stageTime - phaseTime) / Mathf.Max(1e-4f, phaseColorBlendSec));
        return u * u * (3f - 2f * u);
    }

    /// <summary>切替の瞬間のフラッシュ量 0..phaseFlashAmount（線形に減衰）。</summary>
    public float PhaseFlash(float stageTime)
    {
        if (phase == StageCgPhaseKind.None || phaseFlashSec <= 0f || phaseFlashAmount <= 0f) return 0f;
        float t = stageTime - phaseTime;
        if (t < 0f || t >= phaseFlashSec) return 0f;
        return phaseFlashAmount * (1f - t / phaseFlashSec);
    }

    /// <summary>ボス個体の奥行き。トラックが無ければ bossDepth。</summary>
    public float BossDepthAt(string bossId, float stageTime)
    {
        if (bossDepthTracks == null || string.IsNullOrEmpty(bossId)) return bossDepth;
        for (int i = 0; i < bossDepthTracks.Length; i++)
        {
            StageCgBossDepthTrack track = bossDepthTracks[i];
            if (track == null || track.bossId != bossId || track.keys == null || track.keys.Length == 0) continue;
            StageCgBossDepthKey[] k = track.keys;
            if (stageTime <= k[0].time) return k[0].depth;
            for (int j = 1; j < k.Length; j++)
            {
                if (stageTime > k[j].time) continue;
                float span = Mathf.Max(1e-4f, k[j].time - k[j - 1].time);
                float u = Mathf.Clamp01((stageTime - k[j - 1].time) / span);
                return Mathf.Lerp(k[j - 1].depth, k[j].depth, u * u * (3f - 2f * u));
            }
            return k[k.Length - 1].depth;
        }
        return bossDepth;
    }

    /// <summary>ボス個体の明度。上書きが無ければ bossBrightness。</summary>
    public float BossBrightnessAt(string bossId)
    {
        if (bossBrightnessOverrides == null || string.IsNullOrEmpty(bossId)) return bossBrightness;
        for (int i = 0; i < bossBrightnessOverrides.Length; i++)
        {
            StageCgBossBrightnessOverride o = bossBrightnessOverrides[i];
            if (o != null && o.bossId == bossId) return o.brightness;
        }
        return bossBrightness;
    }

    /// <summary>このプロファイルがそのステージのものか。</summary>
    public bool Matches(StageData stage)
    {
        if (stage == null) return false;
        if (!string.IsNullOrEmpty(stageDirectory)
            && !string.IsNullOrEmpty(stage.stageDirectoryName)
            && stage.stageDirectoryName == stageDirectory) return true;
        return !string.IsNullOrEmpty(stageNameFallback) && stage.stageName == stageNameFallback;
    }

    /// <summary>カメラの姿勢補間量 0..1（0=見上げ / 1=通常）。中点で速度最大の smoothstep。</summary>
    public float CameraProgress(float stageTime)
    {
        float u = Mathf.Clamp01((stageTime - cameraHoldEnd) / Mathf.Max(1e-4f, cameraSettleEnd - cameraHoldEnd));
        return u * u * (3f - 2f * u);
    }

    /// <summary>黒 → 空のフェード係数 0..1（0=真っ黒）。</summary>
    public float BlackFade(float stageTime)
    {
        float u = Mathf.Clamp01((stageTime - blackFadeStart) / Mathf.Max(1e-4f, blackFadeEnd - blackFadeStart));
        return u * u * (3f - 2f * u);
    }
}
