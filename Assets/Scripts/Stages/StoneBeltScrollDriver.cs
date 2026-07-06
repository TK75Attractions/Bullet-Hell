using UnityEngine;

/// <summary>
/// 石工ステージのベルトコンベア帯(stone_conveyor_belt)のスリット模様スクロールを、
/// ブロックが帯上を流れている区間(belt_flow 窓)だけ進めるドライバ。
///
/// 背景: BulletIndirectURP.shader / BulletIndirect.shader はベルト帯(scale.x&gt;20 &amp;&amp;
/// scale.y&lt;3.5)の UV を従来 <c>_Time.y</c> で常時スクロールしていた。そのため
/// ブロックが静止している区間でも模様だけが流れ、レビュー @9.3「動いてるときだけ
/// 模様が動くようにして」に反していた。第33便でシェーダを global uniform
/// <c>_StoneBeltScroll</c> に切替え、このドライバが flow 窓の間だけ量を進める
/// (窓外は据え置き=模様も静止)。速度は帯上を流れるブロック(belt_flow ov.x=-9.5)
/// と一致する 0.26 UV/s(0.26*36.5≒9.5)。停止・スロー中は Time.deltaTime が
/// timeScale でスケールされるためブロックと同じく止まる。
///
/// flow 窓は stone.chart.json の belt_flow 系クリップ(石工ベルト流し_1/_2・
/// 石工ランダム落下ベルト流し_1/_2)の発火〜life から算出した実質連続区間
/// [11.25s, 37.60s]。以降の finale(belt_bottom_2, 58s〜)は流れが無いため据え置き
/// = 静止した床模様になる。チャートの belt_flow を retime した場合はここも更新する。
/// </summary>
public class StoneBeltScrollDriver : MonoBehaviour
{
    private const string StoneStageName = "石工";
    private const float ScrollSpeed = 0.26f;   // UV/s。belt_flow(ov.x=-9.5)と一致
    private const float FlowStart = 11.25f;    // 石工ベルト流し_1 出現(7:4)
    private const float FlowEnd = 37.60f;      // 石工ランダム落下ベルト流し_2 退場

    private static readonly int ScrollId = Shader.PropertyToID("_StoneBeltScroll");

    private static StoneBeltScrollDriver instance;
    private float scroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("~StoneBeltScrollDriver");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<StoneBeltScrollDriver>();
    }

    private void Update()
    {
        if (!TryGetStoneTime(out float t))
        {
            // 石工の非プレイ中(タイトル/他ステージ/再スタート前)は 0 に戻す。
            // 他ステージの同ゲート要素へ残量を持ち込まず、次の石工開始を 0 から始める。
            if (scroll != 0f)
            {
                scroll = 0f;
                Shader.SetGlobalFloat(ScrollId, 0f);
            }
            return;
        }
        // flow 窓の間だけ進める。窓外(石工プレイ中)は据え置き=模様を止めたまま
        // 表示(0 に戻すと境界で模様がポップするため、凍結が正しい)。
        if (t >= FlowStart && t <= FlowEnd)
        {
            scroll += ScrollSpeed * Time.deltaTime;
        }
        Shader.SetGlobalFloat(ScrollId, scroll);
    }

    private static bool TryGetStoneTime(out float t)
    {
        t = 0f;
        GManager g = GManager.Control;
        if (g == null || g.state != GManager.GameState.Playing) return false;
        StageReader reader = g.SReader;
        if (reader == null || !reader.IsReady) return false;
        StageData stage = reader.CurrentStage;
        if (stage == null || stage.stageName != StoneStageName) return false;
        t = reader.CurrentTime;
        return true;
    }
}
