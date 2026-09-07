using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 石工ステージの導入（0〜4.73 秒）の時刻表。カメラ（<see cref="StoneCgController"/>）と
/// 自機（<see cref="PlayerController"/>）が同じ数値を見るための唯一の置き場。
///
/// 元は音ハメ指示書 A6TRA（石工 CG 録画 stone_cg1 に打ったもの）。録画 t=0 がステージ t=0.28
/// だったので <b>ステージ秒 = 指示書秒 + 0.28</b> で読み替えている。
///   指示書 0.280 → 0.56 … 黒背景が消えていって空が見える
///   指示書 2.530 → 2.81 … カメラは ease in-out。ここで速度最大
///   指示書 3.791 → 4.07 … 自機を下から登場させる
///   指示書 4.446 → 4.73 … 主人公が出きって操作可能に
///
/// 弾幕の開始（6.67 秒）と弾データそのものには一切触れない。石工の CG リグがシーンに居て
/// 有効なとき（<see cref="Available"/>）だけ働き、他ステージ・2P では常に無効。
/// </summary>
public static class StoneCgIntro
{
    /// <summary>黒から空へのフェード（開始・終了）。</summary>
    public const float BlackFadeStart = 0.56f;
    public const float BlackFadeEnd = 1.30f;

    /// <summary>見上げ姿勢を保つ終わり。ここから通常姿勢へ動き出す。</summary>
    public const float CameraHoldEnd = 1.06f;
    /// <summary>通常姿勢へ着地する時刻。中点 (1.06+4.56)/2 = 2.81 で速度最大になる。</summary>
    public const float CameraSettleEnd = 4.56f;

    /// <summary>自機が画面下から上がり始める時刻（指示書 3.791 + 0.28）。</summary>
    public const float PlayerRiseStart = 4.071f;
    /// <summary>自機が初期位置に着いて操作可能になる時刻（指示書 4.446 + 0.28）。</summary>
    public const float PlayerRiseEnd = 4.726f;
    /// <summary>登場前に自機を置いておく画面外の y。</summary>
    public const float PlayerEnterY = -3f;
    /// <summary>着地点＝通常の開始位置（GManager.PlayerStartY と同じ）。</summary>
    public const float PlayerGoalX = 16f;
    public const float PlayerGoalY = 3f;

    /// <summary>
    /// 石工の CG リグ（<see cref="StoneCgController"/>）がシーンに居て有効な間だけ true。
    /// これが false のときは導入演出そのものが存在しない＝従来どおりの挙動になる。
    /// </summary>
    public static bool Available;

    /// <summary>石工ステージをプレイ中ならステージ時計を返す。</summary>
    public static bool TryGetStageTime(out float stageTime)
    {
        stageTime = 0f;
        if (!Available) return false;
        GManager g = GManager.Control;
        if (g == null || g.state != GManager.GameState.Playing) return false;
        StageReader reader = g.SReader;
        if (reader == null) return false;
        StageData stage = reader.CurrentStage;
        if (stage == null) return false;
        bool match = (!string.IsNullOrEmpty(stage.stageDirectoryName) && stage.stageDirectoryName == "stone")
                     || stage.stageName == "石工";
        if (!match) return false;
        stageTime = reader.CurrentTime;
        return true;
    }

    /// <summary>
    /// 導入中（ステージ時計 &lt; <see cref="PlayerRiseEnd"/>）なら、自機を固定すべき位置と
    /// 表示するかどうかを返す。true の間は入力も被弾も止める。2P では常に false。
    /// </summary>
    public static bool TryGetPlayerEntry(out float2 position, out bool visible)
    {
        position = new float2(PlayerGoalX, PlayerGoalY);
        visible = true;
        GManager g = GManager.Control;
        if (g == null || g.twoPlayer) return false;
        if (!TryGetStageTime(out float t)) return false;
        if (t >= PlayerRiseEnd) return false;

        visible = t >= PlayerRiseStart;
        float u = Mathf.Clamp01((t - PlayerRiseStart) / Mathf.Max(1e-4f, PlayerRiseEnd - PlayerRiseStart));
        float e = 1f - Mathf.Pow(1f - u, 3f);   // ease-out cubic（着地でぴたりと止まる）
        position = new float2(PlayerGoalX, Mathf.Lerp(PlayerEnterY, PlayerGoalY, e));
        return true;
    }

    /// <summary>カメラの姿勢補間量 0..1（0=見上げ / 1=通常）。中点で速度最大の smoothstep。</summary>
    public static float CameraProgress(float stageTime)
    {
        float u = Mathf.Clamp01((stageTime - CameraHoldEnd) / Mathf.Max(1e-4f, CameraSettleEnd - CameraHoldEnd));
        return u * u * (3f - 2f * u);
    }

    /// <summary>黒 → 空のフェード係数 0..1（0=真っ黒）。</summary>
    public static float BlackFade(float stageTime)
    {
        float u = Mathf.Clamp01((stageTime - BlackFadeStart) / Mathf.Max(1e-4f, BlackFadeEnd - BlackFadeStart));
        return u * u * (3f - 2f * u);
    }
}
