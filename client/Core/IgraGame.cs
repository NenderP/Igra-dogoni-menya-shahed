using FontStashSharp;
using Igra.Client.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Text.Json;

namespace Igra.Client.Core;

/// <summary>Главная игра: окно, шрифты, сеть, переключение сцен.</summary>
public class IgraGame : Game
{
    private readonly GraphicsDeviceManager _gfx;
    private SpriteBatch _batch = null!;
    private FontSystem _fonts = null!;
    private Texture2D _bgTex = null!;
    private Texture2D? _whiteTex;
    private MouseState _prevMouse;
    private KeyboardState _prevKeys;
    private bool _clickConsumed;

    public Net.NetClient Net { get; } = new();
    private Scene? _scene;
    public Scene? Scene { get => _scene; set { _scene = value; _fade = 0f; } }
    public string PlayerId { get; private set; }
    public string StatusLine { get; set; } = "";
    public List<string> Feed { get; } = new(); // последние события для меню
    public float Dt { get; private set; }
    private float _fade = 1f; // затемнение при смене сцены (1 → 0)

    public IgraGame()
    {
        _gfx = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        Window.Title = "Igra — карточная";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        PlayerId = LoadOrCreatePlayerId();
    }

    protected override void Initialize()
    {
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _fonts = new FontSystem();
        foreach (var path in FontCandidates.Where(File.Exists))
        {
            try { _fonts.AddFont(File.ReadAllBytes(path)); break; } catch { /* следующий */ }
        }

        // вертикальный градиент фона (1x256, растягивается)
        _bgTex = new Texture2D(GraphicsDevice, 1, 256);
        var px = new Color[256];
        for (int y = 0; y < 256; y++)
        {
            float t = y / 255f;
            px[y] = new Color(
                (byte)(30 + (12 - 30) * t),
                (byte)(34 + (14 - 34) * t),
                (byte)(52 + (22 - 52) * t));
        }
        _bgTex.SetData(px);
        Art.Init(GraphicsDevice);
        Sfx.Init();

        Scene = new ConnectScene(this);
        var _ = Task.Run(async () =>
        {
            try
            {
                await Net.ConnectAsync();
                await Net.SendAsync("hello", new { player_id = PlayerId, display_name = "player" });
            }
            catch (Exception ex)
            {
                StatusLine = "Сервер недоступен: " + ex.Message;
            }
        });
    }

    protected override void Update(GameTime gameTime)
    {
        Dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Fx.Update(Dt);

        var keys = Keyboard.GetState();
        if (keys.IsKeyDown(Keys.M) && !_prevKeys.IsKeyDown(Keys.M))
            Sfx.ToggleMute();
        _prevKeys = keys;

        while (Net.TryTake(out var msg))
            Scene?.OnMessage(msg.Type, msg.Payload);

        Scene?.Update();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _clickConsumed = false;
        GraphicsDevice.Clear(new Color(24, 26, 34));

        _fade = MathF.Min(1f, _fade + Dt * 3f);
        var shake = Fx.ShakeOffset();

        _batch.Begin(transformMatrix: Matrix.CreateTranslation(new Vector3(shake, 0)));
        DrawBackground(_batch);
        Scene?.Draw(_batch, _fonts);
        if (!string.IsNullOrEmpty(StatusLine))
            DrawString(_batch, 16, 700, StatusLine, Color.OrangeRed, 18);
        Fx.Draw(this, _batch);
        _batch.End();

        if (Fx.FlashAlpha > 0.01f)
        {
            _batch.Begin();
            FillRect(_batch, new Rectangle(0, 0, 1280, 720), Fx.FlashColor * Fx.FlashAlpha);
            _batch.End();
        }

        if (_fade < 1f)
        {
            _batch.Begin();
            FillRect(_batch, new Rectangle(0, 0, 1280, 720), Color.Black * (1 - _fade));
            _batch.End();
        }

        _prevMouse = Mouse.GetState();
        base.Draw(gameTime);
    }

    // ---------- Хелперы рисования/ввода ----------

    public void DrawString(SpriteBatch b, float x, float y, string text, Color color, float size = 20f) =>
        b.DrawString(_fonts.GetFont(size), text, new Vector2(x, y), color);

    /// <summary>Шрифт заданного размера (для Fx и внешних рисовалок).</summary>
    public DynamicSpriteFont FontOf(float size) => _fonts.GetFont(size);

    /// <summary>Белый пиксель 1x1 (кэшируется).</summary>
    public Texture2D White => _whiteTex ??= CreateWhite();

    private Texture2D CreateWhite()
    {
        var t = new Texture2D(GraphicsDevice, 1, 1);
        t.SetData(new[] { Color.White });
        return t;
    }

    /// <summary>Рисует фон: картинка из Assets/bg, иначе градиент.</summary>
    public void DrawBackground(SpriteBatch b)
    {
        var art = Art.BattleBg();
        if (art != null) { b.Draw(art, new Rectangle(0, 0, 1280, 720), Color.White); return; }
        b.Draw(_bgTex, new Rectangle(0, 0, 1280, 720), Color.White);
    }

    /// <summary>Панель с рамкой (карточка/кнопка-контейнер).</summary>
    public void Panel(SpriteBatch b, Rectangle r, Color fill, Color? border = null)
    {
        FillRect(b, r, fill);
        if (border != null)
        {
            int t = 2;
            FillRect(b, new Rectangle(r.X, r.Y, r.Width, t), border.Value);
            FillRect(b, new Rectangle(r.X, r.Y + r.Height - t, r.Width, t), border.Value);
            FillRect(b, new Rectangle(r.X, r.Y, t, r.Height), border.Value);
            FillRect(b, new Rectangle(r.X + r.Width - t, r.Y, t, r.Height), border.Value);
        }
    }

    public Vector2 Measure(string text, float size) => _fonts.GetFont(size).MeasureString(text);

    public void FillRect(SpriteBatch b, Rectangle r, Color c) =>
        b.Draw(White, r, c);

    public static bool Clicked(Rectangle r)
    {
        var m = Mouse.GetState();
        return m.LeftButton == ButtonState.Pressed && r.Contains(m.Position);
    }

    /// <summary>Клик засчитывается один раз (на нажатии), а не каждый кадр удержания.</summary>
    public bool ClickOnce(Rectangle r)
    {
        var m = Mouse.GetState();
        bool down = m.LeftButton == ButtonState.Pressed && r.Contains(m.Position);
        bool wasUp = _prevMouse.LeftButton == ButtonState.Released;
        if (down && wasUp && !_clickConsumed)
        {
            _clickConsumed = true;
            return true;
        }
        return false;
    }

    /// <summary>Кнопка: рисует и возвращает true, если по ней кликнули (один раз).</summary>
    public bool Button(SpriteBatch b, Rectangle r, string label, Color? color = null, float fontSize = 22f)
    {
        var m = Mouse.GetState();
        var hovered = r.Contains(m.Position);
        FillRect(b, r, color ?? (hovered ? new Color(70, 90, 130) : new Color(50, 62, 92)));
        var size = Measure(label, fontSize);
        DrawString(b, r.X + (r.Width - size.X) / 2, r.Y + (r.Height - size.Y) / 2, label, Color.White, fontSize);
        if (ClickOnce(r)) { Sfx.Click(); return true; }
        return false;
    }

    public static readonly Color[] RarityColors =
    {
        new(120, 120, 120), new(120, 120, 120), new(120, 120, 120), // 0-2
        new(140, 160, 170),                                          // 3★ серый
        new(150, 90, 200),                                           // 4★ фиолетовый
        new(220, 170, 40)                                            // 5★ золото
    };

    // ---------- player_id между запусками ----------

    private static string LoadOrCreatePlayerId()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "igra_player.txt");
            if (File.Exists(path)) return File.ReadAllText(path).Trim();
            var id = Guid.NewGuid().ToString("N")[..12];
            File.WriteAllText(path, id);
            return id;
        }
        catch
        {
            return Guid.NewGuid().ToString("N")[..12];
        }
    }

    private static readonly string[] FontCandidates =
    {
        "C:\\Windows\\Fonts\\segoeui.ttf",
        "C:\\Windows\\Fonts\\arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/System/Library/Fonts/Helvetica.ttc"
    };
}

/// <summary>Базовая сцена.</summary>
public abstract class Scene(IgraGame game)
{
    protected readonly IgraGame G = game;

    public virtual void Update() { }
    public abstract void Draw(SpriteBatch batch, FontSystem fonts);
    public virtual void OnMessage(string type, JsonElement payload) { }
}
