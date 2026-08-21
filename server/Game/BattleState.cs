namespace Server.Game;

public enum Phase { RoundStart, Action, GameOver }

public class CharacterState
{
    public required string Uid { get; init; }
    public required string DefId { get; init; }
    public required TimeOfDay Element { get; init; }
    public int MaxHp { get; init; }
    public int Hp { get; set; }
    public int EnergyMax { get; init; }
    public int Energy { get; set; }
    public int Attack { get; set; }
    public int Shield { get; set; }
    public bool Alive => Hp > 0;
}

public class PlayerSide
{
    public required string PlayerId { get; init; }
    public required List<CharacterState> Characters { get; init; }
    public int ActiveIndex { get; set; }
    public List<string> Hand { get; } = new();          // def_id карт поддержки
    public List<string> FieldSupports { get; } = new(); // разыгранные карты (v0 — просто счётчик поля)
    public DicePool Dice { get; } = new();
    public int RerollsLeft { get; set; }
    public bool FreeSwapUsed { get; set; }
    public bool EndedTurn { get; set; }

    public CharacterState Active => Characters[ActiveIndex];
    public IEnumerable<CharacterState> Alive => Characters.Where(c => c.Alive);
}

public class BattleState
{
    public required PlayerSide SideA { get; init; }
    public required PlayerSide SideB { get; init; }
    public int Round { get; set; }
    public Phase Phase { get; set; } = Phase.RoundStart;
    public string? WinnerId { get; set; }
    public List<string> Log { get; } = new();

    public PlayerSide Other(PlayerSide side) => ReferenceEquals(side, SideA) ? SideB : SideA;

    public void LogAdd(string msg)
    {
        Log.Add($"[R{Round}] {msg}");
    }
}
