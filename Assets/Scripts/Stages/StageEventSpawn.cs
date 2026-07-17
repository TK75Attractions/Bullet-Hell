using System;

/// <summary>
/// ステージイベントチャンネル(SPEC-RUNTIME-V2.md P1-c)。弾を伴わない演出キューを
/// 時刻指定で発火するための最小データ。弾データには一切影響しない完全加算機能。
/// </summary>
[Serializable]
public struct StageEventSpawn
{
    public string eventName;
    public float time;
}
