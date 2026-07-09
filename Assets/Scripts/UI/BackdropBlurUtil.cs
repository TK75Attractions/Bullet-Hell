using UnityEngine;

// 背景ぼかしの共通ユーティリティ。難易度オーバーレイ(JsabStageSelect.CaptureBlurBackground)
// と同じ「2x ずつのダウンサンプルピラミッド + 1/4 解像度保持」方式でぼけを作る。
// 一足飛びの縮小や固定半径のシェーダ blur はサンプル飛ばしでモザイク状のジャギー
// (ブロックノイズ)になるため、全段をバイリニア 2x2 平均で畳んでガウシアン近似の
// 滑らかなぼけにする。設定/引き継ぎ画面の背景を難易度側と同一品質に統一する。
public static class BackdropBlurUtil
{
    // 現在の画面をキャプチャし、ピラミッドぼかし済みの 1/4 解像度 RenderTexture を返す。
    // WaitForEndOfFrame の後(=描画完了後)に呼ぶこと。返した RT は呼び出し側が破棄する。
    public static RenderTexture CapturePyramidBlur()
    {
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        RenderTexture rt = BuildPyramidBlur(shot);
        Object.Destroy(shot);
        return rt;
    }

    // 与えられたスクリーンショットからピラミッドぼかし RT を生成する。
    public static RenderTexture BuildPyramidBlur(Texture2D shot)
    {
        int w4 = Mathf.Max(24, shot.width / 4);
        int h4 = Mathf.Max(14, shot.height / 4);
        RenderTexture blurRT = new RenderTexture(w4, h4, 0) { filterMode = FilterMode.Bilinear };
        RenderTexture half = RenderTexture.GetTemporary(Mathf.Max(2, shot.width / 2), Mathf.Max(2, shot.height / 2), 0);
        RenderTexture quarter = RenderTexture.GetTemporary(w4, h4, 0);
        RenderTexture eighth = RenderTexture.GetTemporary(Mathf.Max(2, shot.width / 8), Mathf.Max(2, shot.height / 8), 0);
        RenderTexture sixteenth = RenderTexture.GetTemporary(Mathf.Max(2, shot.width / 16), Mathf.Max(2, shot.height / 16), 0);
        half.filterMode = quarter.filterMode = eighth.filterMode = sixteenth.filterMode = FilterMode.Bilinear;
        Graphics.Blit(shot, half);
        Graphics.Blit(half, quarter);
        Graphics.Blit(quarter, eighth);
        Graphics.Blit(eighth, sixteenth);
        Graphics.Blit(sixteenth, eighth);
        Graphics.Blit(eighth, blurRT);
        RenderTexture.ReleaseTemporary(half);
        RenderTexture.ReleaseTemporary(quarter);
        RenderTexture.ReleaseTemporary(eighth);
        RenderTexture.ReleaseTemporary(sixteenth);
        return blurRT;
    }

    // RenderTexture を安全に解放する共通ヘルパー。
    public static void ReleaseRT(ref RenderTexture rt)
    {
        if (rt != null)
        {
            rt.Release();
            Object.Destroy(rt);
            rt = null;
        }
    }
}
