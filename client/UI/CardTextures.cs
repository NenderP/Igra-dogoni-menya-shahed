using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Graphics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace Igra.Client.UI;

/// <summary>
/// Ресурсы для Gum-компонентов: скруглённый квадрат, мягкая тень, запасная иконка «?»
/// и растровый BMFont (генерируются один раз из System.Drawing, кэшируются).
/// </summary>
public static class CardTextures
{
    private static GraphicsDevice _gd = null!;
    private static bool _inited;
    private static BitmapFont? _font;
    private static string? _fontDir;

    /// <summary>Ширины символов атласа в пикселях — заполняются при растеризации.</summary>
    private static readonly Dictionary<int, float> _charWidths = new();

    /// <summary>Надёжная ширина символа для переносов (из данных собственного атласа).</summary>
    public static float CharWidth(char c) => _charWidths.TryGetValue(c, out var w) ? w : 12f;

    public static Texture2D Rounded { get; private set; } = null!;
    public static Texture2D Shadow { get; private set; } = null!;
    public static Texture2D Unknown { get; private set; } = null!;

    /// <summary>BMFont с кириллицей для TextRuntime (создаётся лениво).</summary>
    public static BitmapFont Font => _font ??= BuildFont();

    public static void Init(GraphicsDevice gd)
    {
        if (_inited) return;
        _gd = gd;
        Rounded = CreateRounded(64, 8);
        Shadow = CreateShadow(128);
        Unknown = CreateUnknown();
        _inited = true;
    }

    // ---------- формы ----------

    private static Texture2D CreateRounded(int size, int rad)
    {
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.White);
        g.FillPath(brush, RoundedPath(0.5f, size, rad));
        return ToTexture(bmp);
    }

    private static Texture2D CreateShadow(int size)
    {
        using var bmp = new Bitmap(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                float r = MathF.Sqrt(dx * dx + dy * dy);
                float a = Math.Clamp(1f - r, 0f, 1f);
                byte alpha = (byte)(255 * a * a);
                bmp.SetPixel(x, y, Color.FromArgb(alpha, 0, 0, 0));
            }
        return ToTexture(bmp);
    }

    private static Texture2D CreateUnknown()
    {
        using var bmp = new Bitmap(148, 148);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var fill = new SolidBrush(System.Drawing.Color.FromArgb(50, 52, 60));
        g.FillPath(fill, RoundedPath(1f, 148, 12));
        using var f = new Font("Segoe UI", 56, FontStyle.Bold, GraphicsUnit.Pixel);
        var sz = g.MeasureString("?", f);
        using var tb = new SolidBrush(System.Drawing.Color.FromArgb(120, 122, 132));
        g.DrawString("?", f, tb, (148 - sz.Width) / 2f, (148 - sz.Height) / 2f - 4);
        return ToTexture(bmp);
    }

    private static GraphicsPath RoundedPath(float scale, int size, int rad)
    {
        var path = new GraphicsPath();
        float w = size * scale - 1, h = size * scale - 1;
        float r2 = rad * 2f;
        path.AddArc(0, 0, r2, r2, 180, 90);
        path.AddArc(w - r2, 0, r2, r2, 270, 90);
        path.AddArc(w - r2, h - r2, r2, r2, 0, 90);
        path.AddArc(0, h - r2, r2, r2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Texture2D ToTexture(Bitmap bmp)
    {
        var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        return Texture2D.FromStream(_gd, ms);
    }

    // ---------- BMFont с кириллицей ----------

    private const string Chars =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя«»—…?!№" +
        "？"; // U+FF1F — большой знак вопроса для карточек без портрета

    private static BitmapFont BuildFont()
    {
        const int px = 19, atlasW = 512;
        _fontDir = Path.Combine(Path.GetTempPath(), "igra_gum_font");
        Directory.CreateDirectory(_fontDir);
        string pngPath = Path.Combine(_fontDir, "cardfont.png");
        string fntPath = Path.Combine(_fontDir, "cardfont.fnt");

        bool stale = _charWidths.Count == 0;
        if (!File.Exists(pngPath) || !File.Exists(fntPath) || stale)
            Rasterize(px, atlasW, pngPath, fntPath);

        return new BitmapFont(fntPath);
    }

    private static void Rasterize(int px, int atlasW, string pngPath, string fntPath)
    {
        using var font = TryLoadSegoe() ?? new Font(FontFamily.GenericSansSerif, px, FontStyle.Regular, GraphicsUnit.Pixel);
        _charWidths.Clear();

        using var atlas = new Bitmap(atlasW, 512);
        using (var g = Graphics.FromImage(atlas))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            float lineHeight = font!.GetHeight(g);
            float ascent = lineHeight * 0.82f;

            var sb = new StringBuilder();
            sb.AppendLine("info face=\"CardFont\" size=" + px);
            sb.AppendLine($"common lineHeight={MathF.Ceiling(lineHeight)} base={MathF.Ceiling(ascent)} scaleW={atlasW} scaleH=512 pages=1");
            sb.AppendLine("page id=0 file=\"cardfont.png\"");
            sb.AppendLine($"chars count={Chars.Length}");

            int cx = 1, cy = 1, rowH = 0;
            using var bigFont = new Font(font.FontFamily, 62, FontStyle.Regular, GraphicsUnit.Pixel);
            foreach (var ch in Chars)
            {
                bool big = ch == '？';
                using var f = big
                    ? new Font(font.FontFamily, 62, FontStyle.Regular, GraphicsUnit.Pixel)
                    : (Font)null!;
                var drawFont = big ? bigFont : font;
                string s = ch.ToString();
                var sz = g.MeasureString(s, drawFont);
                int w = Math.Max(1, (int)MathF.Ceiling(sz.Width)), h = Math.Max(1, (int)MathF.Ceiling(sz.Height));
                if (cx + w >= atlasW) { cx = 1; cy += rowH + 1; rowH = 0; }
                g.DrawString(s, drawFont, Brushes.White, cx, cy);
                float adv = MathF.Max(w, px * 0.32f);
                _charWidths[(int)ch] = adv;
                sb.AppendLine(
                    $"char id={(int)ch} x={cx} y={cy} width={w} height={h} " +
                    $"xoffset=0 yoffset={Math.Max(0, MathF.Round(ascent - h * 0.86f))} xadvance={adv} page=0 chnl=15");
                cx += w + 1;
                rowH = Math.Max(rowH, h);
            }
            File.WriteAllText(fntPath, sb.ToString());
        }
        atlas.Save(pngPath, ImageFormat.Png);
    }

    private static Font? TryLoadSegoe()
    {
        try
        {
            var pfc = new System.Drawing.Text.PrivateFontCollection();
            pfc.AddFontFile(@"C:\Windows\Fonts\segoeui.ttf");
            return new Font(pfc.Families[0], 19, FontStyle.Regular, GraphicsUnit.Pixel);
        }
        catch { return null; }
    }
}
