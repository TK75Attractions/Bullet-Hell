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
    [Tooltip("1 拍の長さ（秒）。石工 BPM144 = 0.4166667。")]
    public float beatSec = 60f / 144f;
    [Tooltip("拍頭で発光グループ 1・2 を何割増やすか。")]
    [Range(0f, 1f)] public float beatPulse = 0.2f;
    [Tooltip("拍頭の増分が戻るまでの時間。")]
    public float beatDecaySec = 0.15f;

    [Header("形態変化")]
    public StageCgPhaseKind phase = StageCgPhaseKind.None;
    [Tooltip("フェーズが切り替わるステージ秒。石工 72.94 / 艦長 35.2 / 浮浪者 45.714。")]
    public float phaseTime = 72.94f;
    [Tooltip("p1_* → p2_* のクロスフェードにかける時間。")]
    public float phaseCrossfadeSec = 0.5f;
    [Tooltip("第 2 フェーズで隠す既存オブジェクトの名前（前方一致）。")]
    public string[] hideInPhase2 = new string[0];

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
