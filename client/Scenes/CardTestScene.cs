using FontStashSharp;
using Igra.Client.Core;
using Igra.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using RenderingLibrary;

namespace Igra.Client.Scenes;

/// <summary>
/// Тестовая сцена CharacterCardView (запуск: IGRA_CARDTEST=1).
/// Показывает карточки всех пяти стихий + кейс без портрета и с длинным именем.
/// </summary>
public class CardTestScene(IgraGame game) : Scene(game)
{
    private static bool _gumInited;
    private readonly List<CharacterCardView> _cards = new();

    public override void Draw(SpriteBatch batch, FontSystem fonts)
    {
        G.FillRect(batch, new Rectangle(0, 0, 1280, 720), new Color(24, 26, 34));
        G.DrawString(batch, 380, 24, "CharacterCardView — тест", Color.Gold, 30);

        EnsureGum();
        if (_cards.Count == 0) BuildCards();

        // Gum рисуется вне общего SpriteBatch
        batch.End();
        GumService.Default.Update(new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(G.Dt)));
        GumService.Default.Draw();
        batch.Begin();

        G.DrawString(batch, 60, 700, "Рамка = цвет стихии (Theme), скругление 8px, тень под карточкой. Экран тестовый, в меню не включён.",
            Color.Silver, 15);
    }

    private void BuildCards()
    {
        (string el, string defId)[] row =
        {
            ("eclipse", "char_eclipse_sovereign"),
            ("dawn", "char_dawn_herald"),
            ("day", "char_day_mage"),
            ("twilight", "char_twilight_trickster"),
            ("night", "char_night_assassin"),
        };

        for (int i = 0; i < row.Length; i++)
        {
            var card = new CharacterCardView
            {
                X = 60 + i * 234,
                Y = 120,
                Element = row[i].el,
                Title = Ru.Name(row[i].defId),
                Portrait = Art.Portrait(row[i].defId)
            };
            GumService.Default.Root.Children.Add(card);
            _cards.Add(card);
        }

        // нижний ряд: без портрета (fallback «?»), длинное имя, узкая карточка
        var noPortrait = new CharacterCardView { X = 60, Y = 400, Element = "night", Title = "Без портрета" };
        var longName = new CharacterCardView
        {
            X = 320, Y = 400, Element = "dawn",
            Title = "Владыка Вселенского Затмения IX",
            Portrait = Art.Portrait("char_dawn_herald")
        };
        var narrow = new CharacterCardView { X = 580, Y = 410, Element = "day", Title = "Маг Дня" };
        narrow.Width = 160;
        narrow.Height = 200;
        narrow.Refresh();
        GumService.Default.Root.Children.Add(noPortrait);
        GumService.Default.Root.Children.Add(longName);
        GumService.Default.Root.Children.Add(narrow);
        _cards.AddRange(new[] { noPortrait, longName, narrow });
    }

    private void EnsureGum()
    {
        if (_gumInited) return;
        _gumInited = true;

        CardTextures.Init(G.GraphicsDevice);
        var managers = new SystemManagers();
        managers.Initialize(G.GraphicsDevice, false);
        SystemManagers.Default = managers;
        GumService.Default.Initialize(G, managers);
        GumService.Default.CanvasWidth = 1280;
        GumService.Default.CanvasHeight = 720;
    }
}
