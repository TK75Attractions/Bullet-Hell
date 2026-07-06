using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// プレイ開始遷移(PixelTransition の WhiteoutCover→MosaicReveal)を Play Mode で
/// 毎フレーム連写し、Assets/Screenshots/transition_&lt;HHmmss&gt;/ に PNG 保存する検証
/// ハーネス。ScreenCapture.CaptureScreenshotAsTexture を WaitForEndOfFrame 後に呼び、
/// 数フレーム遅延せず実フレームを直取りする。カバー(中央発の白ブロック拡大)と解像
/// (中央発の欠け)を目視で証明するために使う。
/// </summary>
public static class TransitionCaptureMenu
{
    private const string MenuPath = "Tools/Bullet Hell/Debug/Capture Play-Start Transition";

    [MenuItem(MenuPath)]
    private static void Capture()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[TransitionCapture] Play Mode に入ってから実行してください。");
            return;
        }
        PixelTransition pt = Resources.FindObjectsOfTypeAll<PixelTransition>()
            .FirstOrDefault(p => p.gameObject.scene.IsValid());
        if (pt == null)
        {
            Debug.LogWarning("[TransitionCapture] シーン内に PixelTransition が見つかりません。");
            return;
        }
        string dir = Path.Combine(Application.dataPath, "Screenshots", $"transition_{System.DateTime.Now:HHmmss}");
        Directory.CreateDirectory(dir);
        pt.StartCoroutine(Run(pt, dir));
        Debug.Log($"[TransitionCapture] 連写開始: {dir}");
    }

    private static IEnumerator Run(PixelTransition pt, string dir)
    {
        // カバー: 白ブロックが中央から外周へ広がって覆う工程。
        var cover = pt.WhiteoutCover();
        int f = 0;
        while (!cover.IsCompleted)
        {
            yield return new WaitForEndOfFrame();
            Save(dir, $"cover_{f:00}");
            f++;
            if (f > 120) break; // 安全弁
        }
        yield return new WaitForEndOfFrame();
        Save(dir, $"cover_{f:00}_full");

        // 解像: 白カバーが中央から欠けてプレイ画面(ここでは背後のタイトル)が現れる。
        var reveal = pt.MosaicReveal();
        f = 0;
        while (!reveal.IsCompleted)
        {
            yield return new WaitForEndOfFrame();
            Save(dir, $"reveal_{f:00}");
            f++;
            if (f > 120) break;
        }
        Debug.Log($"[TransitionCapture] 完了: {dir}");
    }

    private static void Save(string dir, string name)
    {
        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), bytes);
        Object.Destroy(tex);
    }
}
