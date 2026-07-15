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

    public void Configure(RectTransform animatedPart, bool isButton)
    {
        rect = animatedPart;
        basePosition = rect != null ? rect.anchoredPosition : Vector2.zero;
        button = isButton;
        configured = true;
        ApplyFrame();
    }

    private void OnEnable()
    {
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
            rect.anchoredPosition = basePosition + Vector2.down * (4.5f * press);
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = new Vector3(1f + 0.02f * press, 1f - 0.12f * press, 1f);
        }
        else
        {
            // 台座は固定し、軸とノブだけを左右へ倒す。
            float lean = Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.6f);
            rect.anchoredPosition = basePosition;
            rect.localEulerAngles = new Vector3(0f, 0f, -12f * lean);
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
