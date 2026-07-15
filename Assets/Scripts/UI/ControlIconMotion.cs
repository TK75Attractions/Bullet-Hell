using UnityEngine;

// タイトルとチュートリアルで共有する操作アイコンの小さなループアニメーション。
// スティックは左右へ倒れ、ボタンは周期的に押し込まれる。
[DisallowMultipleComponent]
public sealed class ControlIconMotion : MonoBehaviour
{
    private RectTransform rect;
    private Vector2 basePosition;
    private bool button;
    private bool configured;

    public void Configure(Sprite sprite)
    {
        rect = transform as RectTransform;
        basePosition = rect != null ? rect.anchoredPosition : Vector2.zero;
        button = sprite != null && sprite.name.Contains("Button");
        configured = true;
        ApplyFrame();
    }

    private void OnEnable()
    {
        if (rect == null) rect = transform as RectTransform;
        if (rect != null && !configured) basePosition = rect.anchoredPosition;
    }

    private void LateUpdate()
    {
        if (!configured || rect == null) return;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (rect == null) return;
        if (button)
        {
            // 約1.2秒に1回だけ短く押し込む。常時脈動させず操作の意味を優先する。
            float wave = Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.2f));
            float press = wave * wave * wave * wave;
            rect.anchoredPosition = basePosition + Vector2.down * (2.5f * press);
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = new Vector3(1f + 0.03f * press, 1f - 0.14f * press, 1f);
        }
        else
        {
            // 台座ごとの小さな揺れに留め、左右操作を読み取れる速度と角度にする。
            float lean = Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.6f);
            rect.anchoredPosition = basePosition + Vector2.right * (1.8f * lean);
            rect.localEulerAngles = new Vector3(0f, 0f, -7f * lean);
            rect.localScale = Vector3.one;
        }
    }

    private void OnDisable()
    {
        if (rect == null) return;
        rect.anchoredPosition = basePosition;
        rect.localEulerAngles = Vector3.zero;
        rect.localScale = Vector3.one;
    }
}
