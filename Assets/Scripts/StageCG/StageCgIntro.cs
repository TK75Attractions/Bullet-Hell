using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 背景 CG のあるステージの導入（曲頭〜操作可能まで）の時刻表。カメラ
/// （<see cref="StageCgController"/>）と自機（<see cref="PlayerController"/>）が同じ数値を
/// 見るための唯一の入口。数値そのものは各ステージの <see cref="StageCgProfile"/> が持つ。
///
/// 石工の元は音ハメ指示書 A6TRA（録画 stone_cg1 に打ったもの）。録画 t=0 がステージ t=0.28
/// だったので <b>ステージ秒 = 指示書秒 + 0.28</b> で読み替えている。
///   0.56 黒背景が消えて空が見える / 2.81 カメラの速度最大 /
///   4.071 自機が下から登場 / 4.726 操作可能
///
/// 弾幕の開始と弾データそのものには一切触れない。CG リグがシーンに居て、
/// いまプレイ中のステージにプロファイルがあるときだけ働く（<see cref="ActiveProfile"/> が null なら
/// 従来どおりの挙動）。2P では常に無効。
/// </summary>
public static class StageCgIntro
{
    /// <summary>CG リグ（<see cref="StageCgController"/>）がシーンに居て有効な間だけ true。</summary>
    public static bool Available;

    /// <summary>いま表示中のステージのプロファイル。CG が出ていないときは null。</summary>
    public static StageCgProfile ActiveProfile;

    /// <summary>プロファイルのあるステージをプレイ中ならステージ時計を返す。</summary>
    public static bool TryGetStageTime(out float stageTime)
    {
        stageTime = 0f;
        if (!Available || ActiveProfile == null) return false;
        GManager g = GManager.Control;
        if (g == null || g.state != GManager.GameState.Playing) return false;
        StageReader reader = g.SReader;
        if (reader == null) return false;
        StageData stage = reader.CurrentStage;
        if (stage == null || !ActiveProfile.Matches(stage)) return false;
        stageTime = reader.CurrentTime;
        return true;
    }

    /// <summary>
    /// 導入中（ステージ時計 &lt; playerRiseEnd）なら、自機を固定すべき位置と表示するか
    /// どうかを返す。true の間は入力も被弾も止める。2P では常に false。
    /// </summary>
    public static bool TryGetPlayerEntry(out float2 position, out bool visible)
    {
        position = new float2(16f, 3f);
        visible = true;
        GManager g = GManager.Control;
        if (g == null || g.twoPlayer) return false;
        if (!TryGetStageTime(out float t)) return false;
        StageCgProfile p = ActiveProfile;
        if (p == null || !p.playerIntroEnabled) return false;
        position = new float2(p.playerGoalX, p.playerGoalY);
        if (t >= p.playerRiseEnd) return false;

        visible = t >= p.playerRiseStart;
        float u = Mathf.Clamp01((t - p.playerRiseStart) / Mathf.Max(1e-4f, p.playerRiseEnd - p.playerRiseStart));
        float e = 1f - Mathf.Pow(1f - u, 3f);   // ease-out cubic（着地でぴたりと止まる）
        position = new float2(p.playerGoalX, Mathf.Lerp(p.playerEnterY, p.playerGoalY, e));
        return true;
    }
}
