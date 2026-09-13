using UnityEngine;

/// <summary>
/// Minimal, event-driven camera shake. Idle by default: it never reads or writes
/// the transform until <see cref="Trigger"/> is called, so every camera it is
/// attached to behaves exactly as before on any stage that never triggers it.
///
/// On trigger it offsets <c>transform.localPosition</c> by a short, decaying
/// oscillation and, when finished, restores the exact local position it captured
/// at the start of the shake — offsets are always added to that captured rest
/// pose, never accumulated onto the live transform, so there is no drift or
/// leftover offset. The motion is a deterministic damped cosine (vertical-
/// dominant) so the impact reads the same in every recording.
///
/// The shake advances with <see cref="Time.deltaTime"/>, which is 0 while the game
/// is paused (GManager sets <c>Time.timeScale = 0</c>), so a shake naturally
/// freezes during pause and never continues to jitter on a paused frame.
/// </summary>
[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;

    private bool shaking;
    // 第 6 便 (D): ステージ時計から与える「画面の揺れ」チャンネル。被弾などのイベント揺れと
    // 足し合わせて 1 か所でカメラへ書くので、同時に起きても取り合いにならない。
    private static Vector2 stageOffset;
    private bool stageApplied;

    /// <summary>ステージ時計側から毎フレーム与える揺れ（論理ユニット）。0 で無効。</summary>
    public static void SetStageOffset(Vector2 offset) => stageOffset = offset;

    /// <summary>いま与えられているステージ揺れ（検証で読む）。</summary>
    public static Vector2 StageOffset => stageOffset;
    private float duration;
    private float elapsed;
    private float amplitude;
    private float frequency;
    private Vector3 baseLocalPos;

    // A ground-impact shake reads best biased vertically with a lighter, slightly
    // detuned horizontal component. 1.0 vertical, 0.6 horizontal.
    private const float HorizontalScale = 0.6f;

    private void Awake()
    {
        instance = this;
        stageOffset = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Starts (or restarts) a decaying shake on the active CameraShake instance.
    /// No-op when no CameraShake exists in the scene.
    /// </summary>
    /// <param name="amplitude">Peak offset in world units at the start of the shake.</param>
    /// <param name="duration">Total shake length in seconds (decays to zero).</param>
    /// <param name="frequency">Oscillation rate in Hz; higher = sharper, buzzier.</param>
    public static void Trigger(float amplitude, float duration, float frequency = 20f)
    {
        if (instance == null)
        {
            return;
        }
        instance.Begin(amplitude, duration, frequency);
    }

    private void Begin(float amp, float dur, float freq)
    {
        if (amp <= 0f || dur <= 0f)
        {
            return;
        }

        // Capture the rest pose only when starting from idle, so a re-trigger
        // mid-shake still restores to the true original (never a shaken frame).
        // ステージ揺れが当たっている最中は baseLocalPos が既に素の姿勢なので取り直さない
        // （取り直すと揺れぶんが基準へ混ざって残留オフセットになる）。
        if (!shaking && !stageApplied)
        {
            baseLocalPos = transform.localPosition;
        }

        shaking = true;
        amplitude = amp;
        duration = dur;
        frequency = freq;
        elapsed = 0f;
    }

    private void LateUpdate()
    {
        bool hasStage = stageOffset.sqrMagnitude > 1e-10f;

        if (shaking && elapsed >= duration)
        {
            shaking = false;
        }

        if (!shaking && !hasStage)
        {
            if (stageApplied)
            {
                transform.localPosition = baseLocalPos; // exact restore, no residual offset
                stageApplied = false;
            }
            else if (restoreOnce)
            {
                transform.localPosition = baseLocalPos;
                restoreOnce = false;
            }
            return;
        }

        // 揺れが無い状態から始まるときだけ素の姿勢を取り直す。
        if (!shaking && !stageApplied)
        {
            baseLocalPos = transform.localPosition;
        }
        stageApplied = hasStage;

        Vector3 offset = new Vector3(stageOffset.x, stageOffset.y, 0f);

        if (shaking)
        {
            // Quadratic ease-out envelope: full kick on the first frame, quick settle.
            float remaining = 1f - (elapsed / duration);
            float decay = remaining * remaining;

            // Compute the offset at the CURRENT elapsed before advancing it, so the
            // trigger frame renders at elapsed 0 where the damped cosine is at full
            // amplitude — that single full-strength frame is the landing punch.
            // Vertical starts as a downward dip (the slam compresses the view),
            // then oscillates down as the envelope decays.
            float w = elapsed * frequency * (2f * Mathf.PI);
            float oy = -Mathf.Cos(w);
            float ox = Mathf.Cos(w * 0.9f + 1.7f) * HorizontalScale;
            offset += new Vector3(ox, oy, 0f) * (amplitude * decay);

            // Advance after rendering. Clamp the per-frame step so a single frame
            // hitch at the trigger moment (e.g. the landing frame spawning many
            // bullets) cannot skip the whole shake in one step. Normal frames
            // (~0.016s play, 0.033s capture) are well under the cap.
            elapsed += Mathf.Min(Time.deltaTime, 0.05f);
        }

        transform.localPosition = baseLocalPos + offset;
        restoreOnce = true;
    }

    // 直前フレームで何らかのオフセットを書いたか（次に両方 0 になった 1 回だけ素へ戻す）。
    private bool restoreOnce;
}
