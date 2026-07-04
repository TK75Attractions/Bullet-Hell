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
    /// </summary>
    public static void CenterInkVertically(TMP_Text text)
    {
        if (text == null) return;

        RectTransform rt = text.rectTransform;
        if (!baseY.TryGetValue(rt, out float rest))
        {
            rest = rt.anchoredPosition.y;
            baseY[rt] = rest;
        }

        text.ForceMeshUpdate();
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
        if (min == float.MaxValue) return; // nothing visible (e.g. empty string)

        float inkCenter = (min + max) * 0.5f;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rest - inkCenter);
    }
}
