#ifndef PLAYER_PALETTE_SWAP_INCLUDED
#define PLAYER_PALETTE_SWAP_INCLUDED

// プロジェクトは Linear 色空間。sRGB の GIF パレットを Linear 値にしてから比較する。
static const half3 PlayerPaletteSourceColor1 = half3(0.008568h, 0.445201h, 1.0h);       // #17B2FF
static const half3 PlayerPaletteSourceColor1Dark = half3(0.0h, 0.327778h, 0.806953h);  // #009BE8
static const half3 PlayerPaletteSourceColor2 = half3(1.0h, 0.008568h, 0.107023h);      // #FF175C
static const half3 PlayerPaletteLuma = half3(0.2126h, 0.7152h, 0.0722h);

half3 ApplyPlayerPalette(half3 sourceColor, half3 color1, half3 color2)
{
    half color1Weight = max(
        1.0h - smoothstep(0.035h, 0.16h, distance(sourceColor, PlayerPaletteSourceColor1)),
        1.0h - smoothstep(0.035h, 0.16h, distance(sourceColor, PlayerPaletteSourceColor1Dark)));
    half color2Weight = 1.0h - smoothstep(0.035h, 0.16h, distance(sourceColor, PlayerPaletteSourceColor2));

    // 元の明暗を残しつつ、既定色では GIF の RGB を変更しない。
    half color1Brightness = dot(sourceColor, PlayerPaletteLuma) / max(dot(PlayerPaletteSourceColor1, PlayerPaletteLuma), 0.001h);
    half color2Brightness = dot(sourceColor, PlayerPaletteLuma) / max(dot(PlayerPaletteSourceColor2, PlayerPaletteLuma), 0.001h);
    half3 color1Replaced = saturate(sourceColor + (color1 - PlayerPaletteSourceColor1) * color1Brightness);
    half3 color2Replaced = saturate(sourceColor + (color2 - PlayerPaletteSourceColor2) * color2Brightness);

    sourceColor = lerp(sourceColor, color1Replaced, saturate(color1Weight));
    return lerp(sourceColor, color2Replaced, saturate(color2Weight));
}

#endif
