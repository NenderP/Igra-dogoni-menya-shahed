using Microsoft.Xna.Framework.Audio;
using System.IO;
using System.Text;

namespace Igra.Client.Core;

/// <summary>Простой синтез звуков (WAV в памяти) — без внешних файлов.</summary>
public static class Sfx
{
    private static SoundEffect? _click, _start, _skill, _hit, _win, _lose;
    private static bool _ok;

    public static void Init()
    {
        try
        {
            _click = Tone(0.05f, 880, 0.18f);
            _start = Sweep(0.25f, 330, 660, 0.25f);
            _skill = Sweep(0.18f, 240, 520, 0.30f);
            _hit = Noise(0.13f, 0.35f);
            _win = Jingle(new[] { 523, 659, 784, 1047 }, 0.12f, 0.30f);
            _lose = Jingle(new[] { 440, 330, 247, 165 }, 0.14f, 0.30f);
            _ok = true;
        }
        catch
        {
            _ok = false; // звук не критичен — игра работает без него
        }
    }

    public static void Click() => _click?.Play();
    public static void Start() => _start?.Play();
    public static void Skill() => _skill?.Play();
    public static void Hit() => _hit?.Play();
    public static void Win() => _win?.Play();
    public static void Lose() => _lose?.Play();

    // ---------- синтез ----------

    private static SoundEffect Tone(float dur, float freq, float vol, float sweepTo = 0)
    {
        int sr = 44100, n = (int)(sr * dur);
        var s = new short[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = (float)Math.Sin(Math.PI * i / n);
            float f = sweepTo > 0 ? freq + (sweepTo - freq) * (t / dur) : freq;
            s[i] = (short)(env * vol * Math.Sin(2 * Math.PI * f * t) * 32767);
        }
        return FromSamples(sr, s);
    }

    private static SoundEffect Sweep(float dur, float from, float to, float vol) => Tone(dur, from, vol, to);

    private static SoundEffect Noise(float dur, float vol)
    {
        int sr = 44100, n = (int)(sr * dur);
        var rnd = new System.Random();
        var s = new short[n];
        for (int i = 0; i < n; i++)
        {
            float env = (float)Math.Sin(Math.PI * i / n);
            s[i] = (short)(env * vol * (rnd.NextDouble() * 2 - 1) * 32767);
        }
        return FromSamples(sr, s);
    }

    private static SoundEffect Jingle(int[] notes, float noteDur, float vol)
    {
        int sr = 44100;
        var list = new System.Collections.Generic.List<short>();
        foreach (var f in notes)
        {
            int n = (int)(sr * noteDur);
            for (int i = 0; i < n; i++)
            {
                float env = (float)Math.Sin(Math.PI * i / n);
                list.Add((short)(env * vol * Math.Sin(2 * Math.PI * f * (i / (float)sr)) * 32767));
            }
        }
        return FromSamples(sr, list.ToArray());
    }

    private static SoundEffect FromSamples(int sr, short[] samples)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int dataLen = samples.Length * 2;
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataLen);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); bw.Write((short)1); bw.Write((short)1);
        bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataLen);
        foreach (var s in samples) bw.Write(s);
        ms.Position = 0;
        return SoundEffect.FromStream(ms);
    }
}
