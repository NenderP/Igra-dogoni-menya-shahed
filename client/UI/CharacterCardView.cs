using Gum.DataTypes;
using Igra.Client.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;

namespace Igra.Client.UI;

/// <summary>
/// Переиспользуемая Gum-карточка персонажа: портрет, имя, рамка 3px цвета стихии
/// (Theme.OfElement), скругление 8px, мягкая тень под карточкой. Статичная разметка.
/// Имя переносится по словам и ужимается, чтобы влезть максимум в 2 строки
/// внутри ширины карточки. Размер по умолчанию 220x270; после смены размера
/// вызывай Refresh().
/// </summary>
public class CharacterCardView : ContainerRuntime
{
    private const float BorderThickness = 3f;
    private const float MinNameScale = 0.55f;

    private readonly NineSliceRuntime _shadowRing;
    private readonly NineSliceRuntime _border;
    private readonly NineSliceRuntime _fill;
    private readonly SpriteRuntime _portrait;
    private readonly TextRuntime _name;
    private readonly TextRuntime _fallbackQ;
    private string _title = "";

    public string Element
    {
        get => _element;
        set { _element = value; _border.Color = Theme.OfElement(value); }
    }
    private string _element = "day";

    public string Title
    {
        get => _title;
        set { _title = value ?? ""; FitName(); }
    }

    public Texture2D? Portrait
    {
        get => _portrait.Visible ? _portrait.Texture : null;
        set
        {
            if (value == null)
            {
                _portrait.Visible = false;
                _fallbackQ.Visible = true;
            }
            else
            {
                _fallbackQ.Visible = false;
                _portrait.Visible = true;
                _portrait.Texture = value;
            }
            PositionPortrait();
        }
    }

    /// <summary>Пересчитывает рамки, портрет и имя под текущие Width/Height.</summary>
    public void Refresh()
    {
        float w = Width, h = Height;

        _border.Width = w;
        _border.Height = h;
        _fill.Width = w - BorderThickness * 2;
        _fill.Height = h - BorderThickness * 2;

        PositionPortrait();
        FitName();
    }

    private void PositionPortrait()
    {
        float w = Width, h = Height;
        float psize = h < 240 ? 100 : 148;
        _portrait.Width = psize;
        _portrait.Height = psize;
        _portrait.X = (w - psize) / 2f;
        _portrait.Y = 24;

        // «?» по центру зоны портрета (большой глиф ~64px)
        _fallbackQ.X = (w - 64f) / 2f;
        _fallbackQ.Y = 24 + psize / 2f - 40f;
    }

    public CharacterCardView()
    {
        Width = 220;
        Height = 270;

        // мягкая тень: тот же скруглённый шейп, растянутый на весь контейнер,
        // сдвинут вниз и рисуется полупрозрачным чёрным
        _shadowRing = new NineSliceRuntime
        {
            Texture = CardTextures.Rounded,
            TextureLeft = 12, TextureTop = 12, TextureWidth = 40, TextureHeight = 40,
            X = -10, Y = 4,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 0, Green = 0, Blue = 0, Alpha = 70
        };
        Children.Add(_shadowRing);

        // внешний слой = цветная рамка (видимая кайма после наложения заливки)
        _border = new NineSliceRuntime
        {
            Texture = CardTextures.Rounded,
            TextureLeft = 12, TextureTop = 12, TextureWidth = 40, TextureHeight = 40,
            X = 0, Y = 0, Width = 220, Height = 270,
            Color = Theme.OfElement(_element)
        };
        Children.Add(_border);

        // заливка с отступом = толщина рамки
        _fill = new NineSliceRuntime
        {
            Texture = CardTextures.Rounded,
            TextureLeft = 12, TextureTop = 12, TextureWidth = 40, TextureHeight = 40,
            X = BorderThickness, Y = BorderThickness,
            Width = 220 - BorderThickness * 2, Height = 270 - BorderThickness * 2,
            Color = new Color(38, 48, 66)
        };
        Children.Add(_fill);

        _portrait = new SpriteRuntime
        {
            X = (220 - 148) / 2f, Y = 24, Width = 148, Height = 148,
            Texture = CardTextures.Rounded
        };
        Children.Add(_portrait);

        // запасной знак «?» (большой глиф ？ из атласа) если портрета нет
        _fallbackQ = new TextRuntime
        {
            Text = "？",
            BitmapFont = CardTextures.Font,
            FontScale = 1f,
            Red = 150, Green = 152, Blue = 162,
            Visible = false
        };
        Children.Add(_fallbackQ);

        // стартовое состояние — без портрета: показываем «?»
        _portrait.Visible = false;
        _fallbackQ.Visible = true;
        PositionPortrait();

        // запасной знак «?» если портрета нет — текст, а не спрайт: размер предсказуем
        _fallbackQ = new TextRuntime
        {
            Text = "?",
            BitmapFont = CardTextures.Font,
            FontScale = 3.2f,
            Red = 130, Green = 132, Blue = 142,
            Visible = false
        };
        Children.Add(_fallbackQ);

        _name = new TextRuntime
        {
            Text = "",
            BitmapFont = CardTextures.Font,
            X = 14,
            Red = 255, Green = 255, Blue = 255
        };
        Children.Add(_name);
    }

    // ---------- подгонка имени ----------

    private float NameMaxWidth => MathF.Max(60f, Width - 24f);

    private void FitName()
    {
        float maxW = NameMaxWidth;
        _name.X = 12;
        _name.Width = maxW;
        _name.Y = Height - 74f;

        for (float scale = 1f; scale >= MinNameScale - 0.001f; scale -= 0.05f)
        {
            var lines = WrapWords(_title, maxW / scale);
            if (lines.Count <= 2)
            {
                _name.FontScale = scale;
                _name.Text = string.Join("\n", lines);
                return;
            }
        }

        // не влезает даже минимальным шрифтом в 2 строки — режем хвост с «…»
        var all = WrapWords(_title, maxW / MinNameScale);
        string first = all.Count > 0 ? all[0] : "";
        string rest = string.Join(" ", all.Skip(1));
        float lim = maxW / MinNameScale;
        while (rest.Length > 1 && Measure(rest + "…") > lim)
            rest = rest[..^1];
        _name.FontScale = MinNameScale;
        _name.Text = first + "\n" + rest + "…";
    }

    /// <summary>Ширина строки в пикселях атласа (без масштаба).</summary>
    private static float Measure(string s)
    {
        float w = 0;
        foreach (var ch in s) w += CardTextures.CharWidth(ch);
        return w;
    }

    /// <summary>Жадный перенос по словам; слишком длинные слова рубятся по буквам.</summary>
    private List<string> WrapWords(string text, float maxW)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string piece = word;
            while (Measure(piece) > maxW && piece.Length > 1)
            {
                int cut = piece.Length - 1;
                while (cut > 1 && Measure(piece[..cut]) > maxW) cut--;
                if (cur.Length > 0) { lines.Add(cur); cur = ""; }
                lines.Add(piece[..cut]);
                piece = piece[cut..];
            }
            var cand = cur.Length == 0 ? piece : cur + " " + piece;
            if (Measure(cand) <= maxW || cur.Length == 0) cur = cand;
            else { lines.Add(cur); cur = piece; }
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }
}
