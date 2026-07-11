using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Optically centers a TMP label vertically by its rendered ink bounds instead
/// of the font's ascent/descent line box.
///
/// TMP's <c>Middle</c> vertical alignment centers text using the primary font's
/// line metrics. The UI here draws Japanese through the Latin UI fonts' CJK
/// fallback (M PLUS 1 Code), whose ascent (+46) and descent (-10.8) are strongly
/// asymmetric, so the line-box midpoint sits well above the actual glyph ink.
/// The visible result is Japanese button/label text riding ~7-10px high in its
/// box while Latin text looks centered. Measuring the real glyph extents and
/// nudging the rect down by their center brings any string — Latin or CJK — to
/// true optical center.
///
/// The authored rest position is captured on the first call per label, so the
/// method is idempotent (re-calling after a text change re-centers from the same
/// base rather than drifting) and never discards a label's intended placement.
/// </summary>
public static class TmpAlign
{
    // Authored anchored Y per label, captured the first time it is centered.
    private static readonly Dictionary<RectTransform, float> baseY =
        new Dictionary<RectTransform, float>();

    /// <summary>
    /// Vertically centers <paramref name="text"/> by its rendered ink bounds.
    /// Call after the text (and, for auto-sized labels, its size) is assigned.
    /// Returns false when the correction could not be measured (no visible
    /// characters — e.g. the label's canvas is not initialized yet); callers
    /// that build UI during scene load should re-call once the UI is shown.
    /// </summary>
    public static bool CenterInkVertically(TMP_Text text)
    {
        if (text == null) return false;

        RectTransform rt = text.rectTransform;
        if (!baseY.TryGetValue(rt, out float rest))
        {
            rest = rt.anchoredPosition.y;
            baseY[rt] = rest;
        }

        // ignoreActiveState=true: タイトルメニューや引き継ぎパネルはビルド時に
        // まだ非アクティブなことがあり、既定の ForceMeshUpdate() だと文字が
        // 生成されず補正が無言でスキップされる(第30便で実測 8〜11px の上ずれ
        // として発覚)。非アクティブでも必ずメッシュを生成して測る。
        text.ForceMeshUpdate(true, true);
        TMP_TextInfo info = text.textInfo;
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            if (ch.bottomLeft.y < min) min = ch.bottomLeft.y;
            if (ch.topLeft.y > max) max = ch.topLeft.y;
        }
        if (min == float.MaxValue) return false; // nothing visible (e.g. empty string)

        float inkCenter = (min + max) * 0.5f;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rest - inkCenter);
        return true;
    }

    /// <summary>
    /// ルビ(<paramref name="ruby"/>)を、本文(<paramref name="body"/>)の指定語範囲
    /// [<paramref name="wordStart"/>, wordStart+<paramref name="wordLen"/>) に含まれる
    /// <b>漢字グリフだけ</b>の実測 x 範囲の中心へ水平配置する。送り仮名・かな・
    /// ラテン文字はスパンから除外するので、「終わる」に読み「お」を渡しても「終」の
    /// 直上だけに乗る(かな部分の上には出さない)。
    ///
    /// 従来は「等幅全角 42/38/24px を仮定した算術 x + 幅広の中央ボックス」で置いて
    /// いたため、フォールバック合成された CJK グリフの実アドバンスと数 px ずれ、
    /// ルビが漢字の中心から外れて語全体へ広がって見えることがあった。ここでは
    /// TMP の <see cref="TMP_TextInfo.characterInfo"/> から漢字グリフの左右端を実測し、
    /// その中心にルビの中心(pivot.x=0.5 前提)をワールド座標で合わせる。y は呼び出し側の
    /// 既定値を保持する。measure できなかったときは false を返し、呼び出し側の
    /// 既定配置(算術)をそのまま残す。表示前(非アクティブ)でも測れるよう
    /// ForceMeshUpdate(true,true) を使う。ルビは pivot.x=0.5・中央揃え前提。
    /// </summary>
    public static bool PlaceRubyOverKanji(TMP_Text body, RectTransform ruby, int wordStart, int wordLen)
    {
        if (body == null || ruby == null) return false;

        body.ForceMeshUpdate(true, true);
        TMP_TextInfo info = body.textInfo;
        int total = info.characterCount;
        if (total == 0) return false;

        int lo = Mathf.Clamp(wordStart, 0, total);
        int hi = Mathf.Clamp(wordStart + wordLen, 0, total);

        // 範囲内の漢字グリフだけに絞る(送り仮名・かな・ラテンを除外)。
        int firstK = -1, lastK = -1;
        for (int i = lo; i < hi; i++)
        {
            if (IsKanji(info.characterInfo[i].character))
            {
                if (firstK < 0) firstK = i;
                lastK = i;
            }
        }
        // 漢字が無ければ(万一)範囲全体で測る。
        int s = firstK >= 0 ? firstK : lo;
        int e = firstK >= 0 ? lastK : hi - 1;
        if (e < s) return false;

        float minX = float.MaxValue, maxX = float.MinValue;
        for (int i = s; i <= e; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            if (ch.bottomLeft.x < minX) minX = ch.bottomLeft.x;
            if (ch.topRight.x > maxX) maxX = ch.topRight.x;
        }
        if (minX == float.MaxValue) return false;

        // 漢字ブロックの中心(本文ローカル)→ワールド。ルビは pivot.x=0.5 なので
        // ワールド x を合わせれば視覚中心が一致する。y は現状維持。
        float localCenterX = (minX + maxX) * 0.5f;
        Vector3 world = body.rectTransform.TransformPoint(new Vector3(localCenterX, 0f, 0f));
        Vector3 p = ruby.position;
        ruby.position = new Vector3(world.x, p.y, p.z);
        return true;
    }

    // 漢字(CJK 統合漢字・拡張A・互換漢字)と、々〆〇 の繰り返し/記号。
    // ひらがな(U+3040–309F)・カタカナ(U+30A0–30FF)・ラテンは false。
    private static bool IsKanji(char c)
    {
        return (c >= 0x4E00 && c <= 0x9FFF)
            || (c >= 0x3400 && c <= 0x4DBF)
            || (c >= 0xF900 && c <= 0xFAFF)
            || c == 0x3005 || c == 0x3006 || c == 0x3007;
    }
}
