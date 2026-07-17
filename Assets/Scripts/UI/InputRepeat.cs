// 保持系の入力パターンを共通化する純粋構造体(MonoBehaviour 非依存・EditModeテスト可能)。
// ランキングのイニシャル入力(上下ホールドで文字送り)、引き継ぎ入力のB長押し(取消)、
// F2デバッグのランキング全消去(長押しで誤爆防止)で共用する。

// 「held を一定時間押し続けたら1回だけ true を返す」長押し検出。
public struct HoldTrigger
{
    private float heldTime;
    private bool fired;

    public float HeldSeconds => heldTime;

    // held=false の間はリセットする。閾値を跨いだフレームだけ true。
    public bool Tick(bool held, float dt, float thresholdSeconds)
    {
        if (!held)
        {
            heldTime = 0f;
            fired = false;
            return false;
        }
        heldTime += dt;
        if (!fired && heldTime >= thresholdSeconds)
        {
            fired = true;
            return true;
        }
        return false;
    }

    public void Reset()
    {
        heldTime = 0f;
        fired = false;
    }
}

// 「押した瞬間に1回、その後ホールドで initialDelay 後から repeatInterval 毎に発火」する
// 定番のホールドリピート(ランキングのイニシャル文字送り等)。
public struct HoldRepeatTrigger
{
    private float timer;
    private bool active;

    public bool Tick(bool pressedEdge, bool held, float dt, float initialDelay, float repeatInterval)
    {
        if (pressedEdge)
        {
            active = true;
            timer = initialDelay;
            return true;
        }
        if (!held)
        {
            active = false;
            return false;
        }
        if (!active) return false;

        timer -= dt;
        if (timer <= 0f)
        {
            timer += repeatInterval > 0.01f ? repeatInterval : 0.01f;
            return true;
        }
        return false;
    }
}
