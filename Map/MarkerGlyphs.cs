using System.Collections.Generic;
using GK2.MapMarkers.Api;
using UnityEngine;

namespace GK2.MapMarkers.Map
{
    /// <summary>
    /// 11x11 pixel-art glyphs drawn inside parchment tags. Legend:
    /// 'o' ink outline, 'c' category color, 'l' light tint, 'd' dark tint, 'w' wood, '.' transparent.
    /// </summary>
    internal static class MarkerGlyphs
    {
        public const int Size = 11;

        private static readonly Color32 Ink = new Color32(74, 50, 32, 255);
        private static readonly Color32 Wood = new Color32(150, 100, 55, 255);

        private static readonly Dictionary<MarkerGlyph, string[]> Maps = new Dictionary<MarkerGlyph, string[]>
        {
            [MarkerGlyph.Gem] = new[]
            {
                ".....o.....",
                "....olo....",
                "...olcco...",
                "..olcccco..",
                ".olcccccco.",
                "occcccccdco",
                ".occcccddo.",
                "..occcddo..",
                "...ocddo...",
                "....odo....",
                ".....o.....",
            },
            [MarkerGlyph.Pickaxe] = new[]
            {
                "...oooo....",
                ".oollccoo..",
                "occoooocco.",
                "oco..oo.oco",
                "oo..owo..oo",
                "...owo.....",
                "..owo......",
                ".owo.......",
                "owo........",
                "oo.........",
                "...........",
            },
            [MarkerGlyph.Fish] = new[]
            {
                "...........",
                "...........",
                "...ooooo...",
                ".oolcccco.o",
                "olloccccoco",
                "ocooccccco.",
                "occcccccoco",
                ".oocccooo.o",
                "...ooooo...",
                "...........",
                "...........",
            },
            [MarkerGlyph.Pillar] = new[]
            {
                "....ooo....",
                "...olco....",
                "..olccco...",
                "..oooooo...",
                "...olco....",
                "...olco....",
                "...olco....",
                "...olco....",
                "..olccco...",
                ".oooooooo..",
                ".odddddddo.",
            },
            [MarkerGlyph.Cave] = new[]
            {
                "...ooooo...",
                "..olllllo..",
                ".ollooooco.",
                "olloddddoco",
                "olodddddoco",
                "olodddddoco",
                "ocodddddoco",
                "ocodddddoco",
                "ocodddddoco",
                "ooooooooooo",
                "...........",
            },
            [MarkerGlyph.Ladder] = new[]
            {
                ".oo....oo..",
                ".oco..oco..",
                ".ocooooco..",
                ".olllllco..",
                ".ocooooco..",
                ".oco..oco..",
                ".ocooooco..",
                ".olllllco..",
                ".ocooooco..",
                ".oco..oco..",
                ".oo....oo..",
            },
            [MarkerGlyph.Coin] = new[]
            {
                "...ooooo...",
                "..olllllo..",
                ".ollccccdo.",
                "olcccoccddo",
                "olccoooccdo",
                "olcccoccddo",
                "olccoooccdo",
                "olcccoccddo",
                ".ocdcccddo.",
                "..oddddoo..",
                "...ooooo...",
            },
            [MarkerGlyph.Cross] = new[]
            {
                "....ooo....",
                "....olo....",
                "....oco....",
                ".oooocoooo.",
                ".ollccccco.",
                ".oooocoooo.",
                "....oco....",
                "....oco....",
                "....oco....",
                "....odo....",
                "....ooo....",
            },
            [MarkerGlyph.Star] = new[]
            {
                ".....o.....",
                "....olo....",
                "....olo....",
                "ooooolooooo",
                "oclllcccdco",
                ".occcccddo.",
                "..occcddo..",
                ".occdoocdo.",
                ".ocdo.ocdo.",
                "oodo...odoo",
                "ooo.....ooo",
            },
        };

        public static PixelImage Render(MarkerGlyph glyph, Color color)
        {
            if (!Maps.TryGetValue(glyph, out string[] rows))
            {
                rows = Maps[MarkerGlyph.Gem];
            }
            Color32 fill = color;
            Color32 light = Color.Lerp(color, Color.white, 0.45f);
            Color32 dark = Color.Lerp(color, Color.black, 0.45f);
            fill.a = light.a = dark.a = 255;

            var image = new PixelImage(Size, Size);
            for (int y = 0; y < Size; y++)
            {
                string row = rows[y];
                for (int x = 0; x < Size && x < row.Length; x++)
                {
                    switch (row[x])
                    {
                        case 'o': image.Set(x, y, Ink); break;
                        case 'c': image.Set(x, y, fill); break;
                        case 'l': image.Set(x, y, light); break;
                        case 'd': image.Set(x, y, dark); break;
                        case 'w': image.Set(x, y, Wood); break;
                    }
                }
            }
            return image;
        }
    }
}
