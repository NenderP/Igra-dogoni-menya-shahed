using System.Text.Json;

namespace Igra.Client.Net;

/// <summary>Модель состояния боя, распарсенная из state_sync.</summary>
public class BattleView
{
    public int Round { get; set; }
    public string Phase { get; set; } = "action";
    public string MyActiveUid { get; set; } = "";
    public List<CharView> MyChars { get; } = new();
    public List<CharView> FoeChars { get; } = new();
    public List<string> MyHand { get; set; } = new();
    public List<string> MyDice { get; set; } = new();
    public int RerollsLeft { get; set; }
    public string? LastLog { get; set; }

    public static BattleView Parse(JsonElement p)
    {
        var v = new BattleView { Round = p.Int("round"), Phase = p.Str("phase") ?? "action" };
        v.MyActiveUid = p.Str("active_character") ?? "";

        void Fill(JsonElement side, List<CharView> list)
        {
            list.Clear();
            foreach (var c in side.Arr("characters").EnumerateArray())
                list.Add(new CharView
                {
                    Uid = c.Str("uid") ?? "",
                    DefId = c.Str("def_id") ?? "",
                    Hp = c.Int("hp"),
                    MaxHp = c.Int("max_hp"),
                    Energy = c.Int("energy"),
                    EnergyMax = c.Int("energy_max"),
                    Element = c.Str("element") ?? "",
                    Shield = c.Int("shield"),
                    Alive = !c.TryGetProperty("alive", out var a) || a.GetBoolean()
                });
        }

        Fill(p.GetProperty("you"), v.MyChars);
        Fill(p.GetProperty("opponent"), v.FoeChars);

        v.MyHand = p.GetProperty("you").Arr("hand").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        v.MyDice = p.GetProperty("you").Arr("dice").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        v.RerollsLeft = p.GetProperty("you").Int("rerolls_left");
        return v;
    }

    public CharView? Find(string uid) =>
        MyChars.FirstOrDefault(c => c.Uid == uid) ?? FoeChars.FirstOrDefault(c => c.Uid == uid);
}

public class CharView
{
    public string Uid { get; set; } = "";
    public string DefId { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Energy { get; set; }
    public int EnergyMax { get; set; }
    public string Element { get; set; } = "";
    public int Shield { get; set; }
    public bool Alive { get; set; } = true;

    /// <summary>Короткое имя из def_id: char_day_mage → Day Mage.</summary>
    public string ShortName => DefId.Replace("char_", "").Replace('_', ' ');
}
