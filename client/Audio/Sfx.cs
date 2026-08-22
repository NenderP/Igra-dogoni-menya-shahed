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

        Ensure("click", () => Tone(0.05f, 520, 0.18f));
        Ensure("start", () => Tone(0.25f, 220, 0.25f, 440));
        Ensure("skill", () => Tone(0.18f, 160, 0.30f, 380));
        Ensure("hit", () => Noise(0.13f, 0.35f));
        Ensure("win", () => Jingle(new[] { 262, 330, 392, 523 }, 0.14f, 0.30f));
        Ensure("lose", () => Jingle(new[] { 294, 220, 165, 110 }, 0.16f, 0.30f));
        Ensure("pull", () => Tone(0.07f, 700, 0.15f));
        Ensure("rare", () => Tone(0.20f, 400, 0.25f, 900));
        Ensure("epic", () => Jingle(new[] { 392, 494, 587, 784 }, 0.12f, 0.32f));
        Ensure("death", () => Noise(0.28f, 0.30f));
        Ensure("swap", () => Tone(0.08f, 200, 0.20f));
        Ensure("card", () => Tone(0.06f, 420, 0.12f));

        try { InitMusic(); } catch { }
    }

    private static void Ensure(string name, Func<SoundEffect> synth)
    {
        if (!_map.ContainsKey(name)) _map[name] = synth();
    }

    private static SoundEffect LoadWav(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try
        {
            var wav = DecodeWav(bytes);
            if (wav.HasValue)
            {
                var (sr, ch, smp) = wav.Value;

                // проверка на клиппинг: всё выше -1 dBFS приглушаем до -1 dBFS
                float peak = 0;
                foreach (var v in smp) peak = MathF.Max(peak, MathF.Abs(v));
                float db = 20f * MathF.Log10(MathF.Max(peak, 1e-6f));
                if (peak > 0.891f)
                {
                    float g = 0.891f / peak;
                    for (int i = 0; i < smp.Length; i++) smp[i] *= g;
                    db = -1f;
                }

                // короткие фейды по краям убирают щелчки на старте/обрыве
                FadeEdges(smp, sr, 8);

                Console.WriteLine($"[sfx] {Path.GetFileName(path)}: пик {db:F1} dBFS" +
                    (peak > 0.891f ? " -> ограничен до -1 dBFS" : ", чисто"));
                return ToEffectRaw(sr, ch, smp);
            }
        }
        catch { /* не PCM16 — грузим как есть */ }

        using var fs = File.OpenRead(path);
        var ms = new MemoryStream();
        fs.CopyTo(ms);
        ms.Position = 0;
        return SoundEffect.FromStream(ms);
    }

    /// <summary>Разбирает RIFF/WAVE (PCM16, моно/стерео) во float[-1..1]. null — формат не поддержан.</summary>
    private static (int sr, int ch, float[] smp)? DecodeWav(byte[] b)
    {
        if (b.Length < 44 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF") return null;
        int pos = 12;
        short fmt = 0, ch = 0;
        int sr = 0;
        float[]? smp = null;
        while (pos + 8 <= b.Length)
        {
            string id = Encoding.ASCII.GetString(b, pos, 4);
            int sz = BitConverter.ToInt32(b, pos + 4);
            if (sz < 0 || pos + 8 + sz > b.Length) return null;
            if (id == "fmt ")
            {
                fmt = BitConverter.ToInt16(b, pos + 8);
                ch = BitConverter.ToInt16(b, pos + 10);
                sr = BitConverter.ToInt32(b, pos + 12);
            }
            else if (id == "data")
            {
                if (fmt != 1 || (ch != 1 && ch != 2)) return null;
                smp = new float[sz / 2];
                for (int i = 0; i < smp.Length; i++)
                    smp[i] = BitConverter.ToInt16(b, pos + 8 + i * 2) / 32768f;
            }
            pos += 8 + sz;
            if (pos % 2 == 1) pos++;
        }
        return smp != null && sr > 0 ? (sr, ch, smp) : null;
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
        var s = new float[total];

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
                float af = chords[c][step];
                float pluck = MathF.Exp(-((t % (segDur / 8)) * 9));
                v += 0.06f * MathF.Sin(2 * MathF.PI * af * t) * pluck;

                s[start + i] = v * env;
            }
        }

        for (int i = 0; i < s.Length; i++) s[i] *= 0.7f;
        LowPass(s, sr, 1800);
        return ToEffectRaw(sr, 1, s);
    }

    // ---------- примитивы синтеза ----------

    /// <summary>Однополюсный low-pass — глушит «писк» высоких частот.</summary>
    private static void LowPass(float[] s, int sr, float cutoff)
    {
        float dt = 1f / sr;
        float rc = 1f / (2 * MathF.PI * cutoff);
        float a = dt / (rc + dt), y = 0;
        for (int i = 0; i < s.Length; i++) { y += a * (s[i] - y); s[i] = y; }
    }

    /// <summary>Линейные фейды по краям (мс) — убирают щелчки.</summary>
    private static void FadeEdges(float[] s, int sr, float ms)
    {
        int len = Math.Max(1, (int)(sr * ms / 1000f));
        for (int i = 0; i < len && i < s.Length / 2; i++)
        {
            float k = i / (float)len;
            s[i] *= k;
            s[s.Length - 1 - i] *= k;
        }
    }

    /// <summary>Тон: быстрый атак ~6мс, экспоненциальное затухание, тёплая гармоника вместо чистого писка.</summary>
    private static SoundEffect Tone(float dur, float freq, float vol, float sweepTo = 0)
    {
        int sr = 44100, n = (int)(sr * dur);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float att = MathF.Min(1f, t / 0.006f);
            float env = att * MathF.Exp(-t * 7f);
            float f = sweepTo > 0 ? freq + (sweepTo - freq) * (t / dur) : freq;
            s[i] = env * vol * (MathF.Sin(2 * MathF.PI * f * t)
                              + 0.35f * MathF.Sin(4 * MathF.PI * f * t));
        }
        LowPass(s, sr, 2400);
        FadeEdges(s, sr, 8);
        return ToEffectRaw(sr, 1, s);
    }

    /// <summary>Шум: резкий атак, быстрый спад, жёсткий low-pass — глухой удар вместо шипения.</summary>
    private static SoundEffect Noise(float dur, float vol)
    {
        int sr = 44100, n = (int)(sr * dur);
        var rnd = new Random();
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = MathF.Min(1f, t / 0.004f) * MathF.Exp(-t * 14f);
            s[i] = env * vol * (float)(rnd.NextDouble() * 2 - 1);
        }
        LowPass(s, sr, 1400);
        FadeEdges(s, sr, 8);
        return ToEffectRaw(sr, 1, s);
    }

    private static SoundEffect Jingle(int[] notes, float noteDur, float vol)
    {
        int sr = 44100;
        var list = new List<float>();
        foreach (var f in notes)
        {
            int n = (int)(sr * noteDur);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float att = MathF.Min(1f, t / 0.008f);
                float env = att * MathF.Exp(-t * 5f);
                list.Add(env * vol * (MathF.Sin(2 * MathF.PI * f * t)
                                    + 0.3f * MathF.Sin(4 * MathF.PI * f * t)));
            }
        }
        var s = list.ToArray();
        LowPass(s, sr, 2600);
        FadeEdges(s, sr, 8);
        return ToEffectRaw(sr, 1, s);
    }

    /// <summary>Сборка SoundEffect из float-семплов с мягким лимитером на -1 dBFS.</summary>
    private static SoundEffect ToEffectRaw(int sr, int ch, float[] s)
    {
        var outp = new short[s.Length];
        for (int i = 0; i < s.Length; i++)
            outp[i] = (short)(Math.Clamp(s[i], -0.891f, 0.891f) * 32767);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int dataLen = outp.Length * 2;
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataLen);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); bw.Write((short)1); bw.Write((short)ch);
        bw.Write(sr); bw.Write(sr * ch * 2); bw.Write((short)(ch * 2)); bw.Write((short)16);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataLen);
        foreach (var x in outp) bw.Write(x);
        ms.Position = 0;
        return SoundEffect.FromStream(ms);
    }
}
