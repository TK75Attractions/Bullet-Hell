using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// タイトル画面(3D の旅支度の部屋)を実経路で駆動しながら実フレームを連写する検証ハーネス。
///
/// Play Mode 中に <see cref="Run"/> へコマンド列を渡すと、キーボードイベントを
/// InputSystem へ流し込み(= 実際の InputManager 経路を通る)、指定のタイミングで
/// WaitForEndOfFrame 後のバックバッファを PNG 保存する。
/// EditorApplication.update フックからの撮影は白/黒画像になる罠があるため、
/// 必ず Play 内のコルーチンで撮る。
///
/// コマンド(":" 区切り):
///   shot:&lt;name&gt;                 1 枚撮る
///   state:&lt;label&gt;               そのときの state とカメラ姿勢を Saved へ記録
///   tap:&lt;keyName&gt;               1 フレームだけ押す(直後に burst したいとき)
///   burst:&lt;name&gt;:&lt;count&gt;:&lt;fps&gt;  count 枚を fps 間隔で連写(name_00.png ...)
///   key:&lt;keyName&gt;               1 回押して離す(w / s / a / d / space / escape)
///   hold:&lt;keyName&gt;:&lt;seconds&gt;    指定秒だけ押しっぱなしにする
///   wait:&lt;seconds&gt;              待つ
/// </summary>
public static class TitleRoomCapture
{
    public static bool Busy { get; private set; }
    public static string Log { get; private set; } = string.Empty;
    public static readonly List<string> Saved = new List<string>();

    /// <summary>コマンド列を Play 内のコルーチンで実行する。dir は絶対パス。</summary>
    public static bool Run(string dir, string[] commands)
    {
        if (!EditorApplication.isPlaying)
        {
            Log = "Play Mode ではありません";
            return false;
        }
        if (Busy)
        {
            Log = "実行中です";
            return false;
        }
        MonoBehaviour host = Object.FindFirstObjectByType<GManager>();
        if (host == null)
        {
            Log = "GManager が見つかりません";
            return false;
        }
        Directory.CreateDirectory(dir);
        Saved.Clear();
        Busy = true;
        Log = "running";
        host.StartCoroutine(Execute(dir, commands));
        return true;
    }

    private static IEnumerator Execute(string dir, string[] commands)
    {
        foreach (string raw in commands)
        {
            string[] a = raw.Split(':');
            switch (a[0])
            {
                case "shot":
                    yield return new WaitForEndOfFrame();
                    Save(dir, a[1]);
                    break;
                case "burst":
                {
                    // PNG エンコードは 1 枚 100ms 級で、連写のあいだにゲーム時間が
                    // 進みすぎる(0.4 秒の寄りが 3 コマで終わって見える)。撮影中は
                    // Texture2D をメモリに溜めるだけにして、終わってから書き出す。
                    int count = int.Parse(a[2]);
                    float fps = float.Parse(a[3]);
                    float step = 1f / Mathf.Max(1f, fps);
                    List<Texture2D> shots = new List<Texture2D>(count);
                    List<float> stamps = new List<float>(count);
                    for (int i = 0; i < count; i++)
                    {
                        yield return new WaitForEndOfFrame();
                        shots.Add(ScreenCapture.CaptureScreenshotAsTexture());
                        stamps.Add(Time.realtimeSinceStartup);
                        float t = 0f;
                        while (t < step - 0.001f) { t += Time.unscaledDeltaTime; yield return null; }
                    }
                    for (int i = 0; i < shots.Count; i++)
                    {
                        File.WriteAllBytes(Path.Combine(dir, $"{a[1]}_{i:00}.png"), shots[i].EncodeToPNG());
                        Object.Destroy(shots[i]);
                    }
                    float span = stamps.Count > 1 ? stamps[stamps.Count - 1] - stamps[0] : 0f;
                    Saved.Add($"{a[1]} x{shots.Count} span={span:F3}s avg={(shots.Count > 1 ? span / (shots.Count - 1) : 0f) * 1000f:F1}ms");
                    break;
                }
                case "key":
                    yield return PressKey(ToKey(a[1]), 0.05f);
                    break;
                case "tap":
                    // 押した直後から連写したい用。押し→1 フレーム→離す、待ちを入れない。
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(ToKey(a[1])));
                    InputSystem.Update();
                    yield return null;
                    InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                    InputSystem.Update();
                    break;
                case "state":
                    Saved.Add("state " + a[1] + " " + StateLine());
                    break;
                case "hold":
                    yield return PressKey(ToKey(a[1]), float.Parse(a[2]));
                    break;
                case "fps":
                {
                    // 毎フレーム 1/smoothDeltaTime を積んで最小値・平均を出す。
                    float dur = float.Parse(a[1]);
                    float el = 0f; float mn = float.MaxValue; float sum = 0f; int n = 0;
                    while (el < dur)
                    {
                        yield return null;
                        float dt = Time.smoothDeltaTime;
                        el += Time.unscaledDeltaTime;
                        if (dt <= 0f) continue;
                        float f = 1f / dt;
                        mn = Mathf.Min(mn, f); sum += f; n++;
                    }
                    Saved.Add($"fps {dur:F0}s min={mn:F1} avg={(n > 0 ? sum / n : 0f):F1} samples={n}");
                    break;
                }
                case "wait":
                {
                    float t = 0f;
                    float d = float.Parse(a[1]);
                    while (t < d) { t += Time.unscaledDeltaTime; yield return null; }
                    break;
                }
            }
        }
        Busy = false;
        Log = "done: " + Saved.Count + " frames";
    }

    private static IEnumerator PressKey(Key key, float seconds)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        InputSystem.Update();
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        InputSystem.Update();
        yield return null;
        yield return null;
    }

    // 検証用の状態行(カメラ姿勢・寄り具合・選択)。
    private static string StateLine()
    {
        TitleRoomController room = TitleRoomController.Instance;
        GManager gm = Object.FindFirstObjectByType<GManager>();
        return (gm != null ? gm.state.ToString() : "-") + " | " + (room != null ? room.DebugState() : "room=null");
    }

    private static Key ToKey(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "w": return Key.W;
            case "s": return Key.S;
            case "a": return Key.A;
            case "d": return Key.D;
            case "space": return Key.Space;
            case "escape": return Key.Escape;
            default: return Key.Space;
        }
    }

    private static void Save(string dir, string name)
    {
        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Saved.Add(name + " " + tex.width + "x" + tex.height);
        Object.Destroy(tex);
    }
}
