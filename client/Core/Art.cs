using Microsoft.Xna.Framework.Graphics;
using System.IO;

namespace Igra.Client.Core;

/// <summary>
/// Загрузчик PNG-ассетов из папки Assets с фоллбеком на процедурную отрисовку.
/// Хочешь свою ИИ-картинку персонажа — положи PNG в client/Assets/chars/{def_id}.png и перезапусти игру.
/// </summary>
public static class Art
{
    private static GraphicsDevice? _gd;
    private static readonly Dictionary<string, Texture2D?> _cache = new();

    public static void Init(GraphicsDevice gd) => _gd = gd;

    public static Texture2D? Tex(string relPath)
    {
        if (_gd == null) return null;
        if (_cache.TryGetValue(relPath, out var t)) return t;

        Texture2D? result = null;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", relPath);
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                result = Texture2D.FromStream(_gd, fs);
            }
        }
        catch { result = null; }

        _cache[relPath] = result;
        return result;
    }

    public static Texture2D? Portrait(string defId) =>
        Tex("chars" + Path.DirectorySeparatorChar + defId + ".png");

    public static Texture2D? BattleBg() =>
        Tex("bg" + Path.DirectorySeparatorChar + "battle_bg.png");
}
