using Microsoft.Xna.Framework.Audio;
using System.IO;
using System.Text;

namespace Igra.Client.Core;

/// <summary>
/// Звук: сначала пробуем файлы из Assets/sfx/*.wav, чего нет — синтезируем.
/// Музыка: Assets/music/theme.ogg|wav, иначе — синтетический эмбиент-луп.
/// M — выключить/включить звук.
/// </summary>
public static class Sfx
{
    private static readonly Dictionary<string, SoundEffect> _map = new();
    private static SoundEffectInstance? _music;
    public static bool Muted { get; private set; }

    public static void Init()
    {
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Assets", "sfx");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.wav"))
                    _map[Path.GetFileNameWithoutExtension(f)] = LoadWav(f);
        }
        catch { /* без файлов — живём на синтезе */ }

        Ensure("click", () => Tone(0.05f, 880, 0.18f));
        Ensure("start", () => Tone(0.25f, 330, 0.25f, 660));
        Ensure("skill", () => Tone(0.18f, 240, 0.30f, 520));
        Ensure("hit", () => Noise(0.13f, 0.35f));
        Ensure("win", () => Jingle(new[] { 523, 659, 784, 1047 }, 0.12f, 0.30f));
        Ensure("lose", () => Jingle(new[] { 440, 330, 247, 165 }, 0.14f, 0.30f));
        Ensure("pull", () => Tone(0.07f, 1200, 0.15f));
        Ensure("rare", () => Tone(0.20f, 600, 0.25f, 1200));
        Ensure("epic", () => Jingle(new[] { 784, 988, 1175, 1568 }, 0.10f, 0.32f));
        Ensure("death", () => Noise(0.28f, 0.30f));
        Ensure("swap", () => Tone(0.08f, 300, 0.20f));
        Ensure("card", () => Tone(0.06f, 700, 0.12f));

        try { InitMusic(); } catch { }
    }

    private static void Ensure(string name, Func<SoundEffect> synth)
    {
        if (!_map.ContainsKey(name)) _map[name] = synth();
    }

    private static SoundEffect LoadWav(string path)
    {
        using var fs = File.OpenRead(path);
        var ms = new MemoryStream();
        fs.CopyTo(ms);
        ms.Position = 0;
        return SoundEffect.FromStream(ms);
    }

    // ---------- публичные хелперы ----------

    public static void Click() => Play("click");
    public static void Start() => Play("start");
    public static void Skill() => Play("skill");
    public static void Hit() => Play("hit");
    public static void Win() => Play("win");
    public static void Lose() => Play("lose");
    public static void Pull() => Play("pull");
    public static void Rare() => Play("rare");
    public static void Epic() => Play("epic");
    public static void Death() => Play("death");
    public static void SwapSnd() => Play("swap");
    public static void Card() => Play("card");

    private static readonly Dictionary<string, float> _vol = new()
    {
        ["hit"] = 0.40f, ["death"] = 0.42f, ["skill"] = 0.45f, ["epic"] = 0.48f,
        ["win"] = 0.50f, ["lose"] = 0.45f, ["click"] = 0.45f, ["pull"] = 0.5f,
        ["rare"] = 0.45f, ["swap"] = 0.45f, ["card"] = 0.45f, ["start"] = 0.5f
    };

    private static void Play(string name)
    {
        if (Muted) return;
        if (!_map.TryGetValue(name, out var e)) return;
        try
        {
            var inst = e.CreateInstance();
            inst.Volume = _vol.GetValueOrDefault(name, 0.55f);
            inst.Play();
        }
        catch { }
    }

    public static void ToggleMute()
    {
        Muted = !Muted;
        if (_music == null) return;
        if (Muted) _music.Pause(); else _music.Play();
    }

    // ---------- музыка ----------

    private static void InitMusic()
    {
        string mdir = Path.Combine(AppContext.BaseDirectory, "Assets", "music");
        SoundEffect? theme = null;

        if (Directory.Exists(mdir))
            foreach (var f in Directory.GetFiles(mdir))
                try { theme = LoadWav(f); break; } catch
                {
                    try
                    {
                        using var fs = File.OpenRead(f);
                        var ms = new MemoryStream(); fs.CopyTo(ms); ms.Position = 0;
                        theme = SoundEffect.FromStream(ms); break;
                    }
                    catch { }
                }

        theme ??= SynthLoop();

        _music = theme.CreateInstance();
        _music.IsLooped = true;
        _music.Volume = 0.22f;
        _music.Play();
    }

    /// <summary>Спокойный луп ~8с: аккорды Am–F–C–G с арпеджио.</summary>
    private static SoundEffect SynthLoop()
    {
        int sr = 22050;
        float segDur = 2f;
        var chords = new[]
        {
            new[] { 220f, 261.63f, 329.63f },
            new[] { 174.61f, 220f, 261.63f },
            new[] { 196f, 246.94f, 293.66f },
            new[] { 130.81f, 164.81f, 196f },
        };

        int total = (int)(sr * segDur * chords.Length);
        var s = new short[total];

        for (int c = 0; c < chords.Length; c++)
        {
            int start = (int)(c * segDur * sr);
            int n = (int)(segDur * sr);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float tt = t / segDur;
                float env = MathF.Sin(MathF.PI * Math.Clamp(tt, 0f, 1f));
                env *= env;
                float v = 0;

                foreach (var f in chords[c]) v += 0.05f * MathF.Sin(2 * MathF.PI * f * t);
                v += 0.07f * MathF.Sin(2 * MathF.PI * chords[c][0] / 2 * t);

                int step = (int)(t / (segDur / 8)) % 3;
                float af = chords[c][step] * 2;
                float pluck = MathF.Exp(-((t % (segDur / 8)) * 9));
                v += 0.06f * MathF.Sin(2 * MathF.PI * af * t) * pluck;

                s[start + i] = (short)(v * env * 32767 * 0.7f);
            }
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int dataLen = s.Length * 2;
        bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + dataLen);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16);
        bw.Write((short)1); bw.Write((short)1); bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(dataLen);
        foreach (var x in s) bw.Write(x);
        ms.Position = 0;
        return SoundEffect.FromStream(ms);
    }

    // ---------- примитивы синтеза ----------

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

    private static SoundEffect Noise(float dur, float vol)
    {
        int sr = 44100, n = (int)(sr * dur);
        var rnd = new Random();
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
        var list = new List<short>();
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
        foreach (var x in samples) bw.Write(x);
        ms.Position = 0;
        return SoundEffect.FromStream(ms);
    }
}
