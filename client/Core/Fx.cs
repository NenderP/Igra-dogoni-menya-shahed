using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Igra.Client.Core;

/// <summary>Частицы, всплывающие числа, тряска экрана, вспышки — весь «сок» интерфейса.</summary>
public static class Fx
{
    private struct Particle
    {
        public Vector2 Pos, Vel;
        public float Life, MaxLife, Size;
        public Color Col;
    }

    private struct FText
    {
        public Vector2 Pos;
        public string Text;
        public Color Col;
        public float Life, Size;
    }

    private static readonly List<Particle> _parts = new();
    private static readonly List<FText> _texts = new();
    private static readonly Random _rnd = new();

    public static float ShakeMag { get; private set; }
    private static float _shakeLeft;
    public static float FlashAlpha { get; private set; }
    public static Color FlashColor { get; private set; } = Color.White;

    public static void Burst(Vector2 pos, Color col, int count = 14, float speed = 150f)
    {
        for (int i = 0; i < count; i++)
        {
            double a = _rnd.NextDouble() * Math.PI * 2;
            float v = speed * (0.4f + (float)_rnd.NextDouble() * 0.9f);
            _parts.Add(new Particle
            {
                Pos = pos,
                Vel = new Vector2((float)Math.Cos(a) * v, (float)Math.Sin(a) * v - 40),
                Life = 0.35f + (float)_rnd.NextDouble() * 0.45f,
                MaxLife = 0.8f,
                Size = 2 + (float)_rnd.NextDouble() * 4,
                Col = col
            });
        }
        if (_parts.Count > 600) _parts.RemoveRange(0, _parts.Count - 600);
    }

    public static void FloatText(Vector2 pos, string text, Color col, float size = 24)
    {
        _texts.Add(new FText { Pos = pos, Text = text, Col = col, Life = 1.2f, Size = size });
    }

    public static void Shake(float mag, float time = 0.18f)
    {
        ShakeMag = MathF.Max(ShakeMag, mag);
        _shakeLeft = MathF.Max(_shakeLeft, time);
    }

    public static void Flash(Color col, float alpha) { FlashColor = col; FlashAlpha = alpha; }

    public static void Update(float dt)
    {
        if (_shakeLeft > 0) { _shakeLeft -= dt; if (_shakeLeft <= 0) ShakeMag = 0; }
        FlashAlpha = MathF.Max(0, FlashAlpha - dt * 2.5f);

        for (int i = _parts.Count - 1; i >= 0; i--)
        {
            var p = _parts[i];
            p.Vel.Y += 220 * dt;
            p.Pos += p.Vel * dt;
            p.Life -= dt;
            if (p.Life <= 0) _parts.RemoveAt(i); else _parts[i] = p;
        }
        for (int i = _texts.Count - 1; i >= 0; i--)
        {
            var t = _texts[i];
            t.Pos.Y -= 34 * dt;
            t.Life -= dt;
            if (t.Life <= 0) _texts.RemoveAt(i); else _texts[i] = t;
        }
    }

    /// <summary>Случайное смещение камеры для тряски.</summary>
    public static Vector2 ShakeOffset()
    {
        if (ShakeMag <= 0) return Vector2.Zero;
        return new Vector2((float)(_rnd.NextDouble() * 2 - 1), (float)(_rnd.NextDouble() * 2 - 1)) * ShakeMag;
    }

    public static void Draw(IgraGame g, SpriteBatch b)
    {
        foreach (var p in _parts)
        {
            var c = p.Col * (p.Life / p.MaxLife);
            int s = (int)(p.Size * (0.5f + p.Life / p.MaxLife));
            b.Draw(g.White, new Rectangle((int)p.Pos.X, (int)p.Pos.Y, s + 2, s + 2), c);
        }
        foreach (var t in _texts)
        {
            var c = t.Col * Math.Clamp(t.Life / 1.2f, 0f, 1f);
            var size = g.Measure(t.Text, t.Size);
            b.DrawString(g.FontOf(t.Size), t.Text, t.Pos - size / 2, c);
        }
    }
}
