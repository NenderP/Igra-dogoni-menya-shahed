using Gacha;
using Server.Game;

namespace Server.Game;

/// <summary>
/// Движок боя v0: раунды, дайсы, реакции, энергия, карты поддержки.
/// Server-authoritative: всё состояние здесь, наружу только методы-действия и BattleState.
/// Интеграция с /gacha: колоды приходят как BotDeck (IBotDeckProvider), справочник def_id — /shared/content.md.
/// </summary>
public class BattleEngine
{
    private readonly Random _rng;
    public BattleState State { get; }

    public const int DicePerRound = 8;      // БАЛАНС-TBD
    public const int SkillCost = 2;         // БАЛАНС-TBD
    public const int UltBonusDamage = 3;    // БАЛАНС-TBD
    public const int EnergyPerRoundTick = 1;

    public BattleEngine(string idA, string idB, BotDeck deckA, BotDeck deckB, Random? rng = null)
    {
        _rng = rng ?? new Random();
        State = new BattleState
        {
            SideA = BuildSide(idA, deckA),
            SideB = BuildSide(idB, deckB)
        };
        State.LogAdd($"Бой начался: {idA} vs {idB}");
    }

    private static PlayerSide BuildSide(string playerId, BotDeck deck)
    {
        var chars = deck.Characters.Select(defId =>
        {
            var def = CharacterCatalog.Get(defId);
            return new CharacterState
            {
                Uid = $"{playerId}:{defId}",
                DefId = def.DefId,
                Element = def.Element,
                MaxHp = def.Hp,
                Hp = def.Hp,
                EnergyMax = def.EnergyMax,
                Attack = CharacterCatalog.DefaultAttack(def.Rarity)
            };
        }).ToList();

        return new PlayerSide { PlayerId = playerId, Characters = chars };
    }

    // ---------- Фазы раунда ----------

    public void StartRound()
    {
        State.Round++;
        State.Phase = Phase.Action;
        foreach (var side in new[] { State.SideA, State.SideB })
        {
            side.RerollsLeft = 1;
            side.FreeSwapUsed = false;
            side.EndedTurn = false;
            side.Hand.Add(SupportCatalog.AllIds[_rng.Next(SupportCatalog.AllIds.Length)]); // раздача поддержки
            side.Dice.Roll(DicePerRound, _rng);
        }
        State.LogAdd($"--- Раунд {State.Round}: раздача поддержки, брошено по {DicePerRound} дайсов ---");
    }

    /// <summary>Переброс части дайсов. Один за раунд.</summary>
    public bool Reroll(PlayerSide side, IReadOnlyList<int> indexes)
    {
        if (State.Phase != Phase.Action || side.RerollsLeft <= 0 || indexes.Count == 0) return false;
        side.Dice.Reroll(indexes, _rng);
        side.RerollsLeft--;
        State.LogAdd($"{side.PlayerId} перебросил {indexes.Count} дайсов");
        return true;
    }

    // ---------- Действия ----------

    public bool PlaySupport(PlayerSide side, string cardDefId, int? dieIndexToFix = null)
    {
        if (State.Phase != Phase.Action) return false;
        if (!side.Hand.Contains(cardDefId)) return false;
        if (!side.Dice.Pay(TimeOfDay.Omni, 1) && !PayAny(side, 1)) return false;

        var def = SupportCatalog.Get(cardDefId);
        var target = side.Active;
        switch (def.Effect)
        {
            case SupportEffect.Shield:
                target.Shield += def.Amount;
                State.LogAdd($"{side.PlayerId}: {cardDefId} → щит {def.Amount} на {target.DefId}");
                break;
            case SupportEffect.Heal:
                target.Hp = Math.Min(target.MaxHp, target.Hp + def.Amount);
                State.LogAdd($"{side.PlayerId}: {cardDefId} → хил {def.Amount} на {target.DefId}");
                break;
            case SupportEffect.EnergyBoost:
                target.Energy = Math.Min(target.EnergyMax, target.Energy + def.Amount);
                State.LogAdd($"{side.PlayerId}: {cardDefId} → +{def.Amount} энергии {target.DefId}");
                break;
            case SupportEffect.ExtraReroll:
                side.RerollsLeft++;
                State.LogAdd($"{side.PlayerId}: {cardDefId} → доп. переброс");
                break;
            case SupportEffect.FixDie:
                int idx = dieIndexToFix ?? side.Dice.Dice.FindIndex(d => d != TimeOfDay.Omni);
                if (!side.Dice.FixToOmni(idx)) return false;
                State.LogAdd($"{side.PlayerId}: {cardDefId} → дайс стал Omni");
                break;
        }
        side.Hand.Remove(cardDefId);
        side.FieldSupports.Add(cardDefId);
        side.EndedTurn = false;
        return true;
    }

    public bool UseSkill(PlayerSide attackerSide, CharacterState attacker, CharacterState defender)
    {
        if (State.Phase != Phase.Action || !attacker.Alive || !defender.Alive) return false;
        if (!attackerSide.Dice.Pay(attacker.Element, SkillCost))
        {
            if (!attackerSide.Dice.Pay(TimeOfDay.Omni, SkillCost)) return false;
        }

        attacker.Energy = Math.Min(attacker.EnergyMax, attacker.Energy + 1); // скилл даёт энергию
        DealDamage(attackerSide, attacker, defender, attacker.Attack, "скилл");
        attackerSide.EndedTurn = false;
        return true;
    }

    public bool UseUltimate(PlayerSide attackerSide, CharacterState attacker, CharacterState defender)
    {
        if (State.Phase != Phase.Action || !attacker.Alive || !defender.Alive) return false;
        if (attacker.Energy < attacker.EnergyMax) return false;

        attacker.Energy = 0;
        DealDamage(attackerSide, attacker, defender, attacker.Attack + UltBonusDamage, "ульта");
        attackerSide.EndedTurn = false;
        return true;
    }

    public bool Swap(PlayerSide side, string toUid)
    {
        if (State.Phase != Phase.Action) return false;
        if (side.FreeSwapUsed) return false; // v0: одна бесплатная смена за раунд

        int idx = side.Characters.FindIndex(c => c.Uid == toUid);
        if (idx < 0 || !side.Characters[idx].Alive || idx == side.ActiveIndex) return false;

        side.ActiveIndex = idx;
        side.FreeSwapUsed = true;
        State.LogAdd($"{side.PlayerId}: активен теперь {toUid}");
        side.EndedTurn = false;
        return true;
    }

    public bool EndTurn(PlayerSide side)
    {
        if (State.Phase != Phase.Action) return false;
        side.EndedTurn = true;
        State.LogAdd($"{side.PlayerId} закончил ход");

        var other = State.Other(side);
        if (other.EndedTurn) EndRound();
        return true;
    }

    // ---------- Урон и смерти ----------

    private void DealDamage(PlayerSide attackerSide, CharacterState attacker, CharacterState defender, int baseDamage, string source)
    {
        int dmg = baseDamage;
        string reactionNote = "";
        var reaction = Reactions.Resolve(attacker.Element, defender.Element);
        if (reaction is { } r)
        {
            dmg = (int)Math.Round((dmg + r.BonusDamage) * r.Multiplier);
            reactionNote = $" | Реакция {r.Name}!";
        }

        int toShield = Math.Min(defender.Shield, dmg);
        defender.Shield -= toShield;
        int toHp = dmg - toShield;
        defender.Hp -= toHp;

        State.LogAdd(
            $"{attackerSide.PlayerId}: {attacker.DefId} ({source}) бьёт {defender.DefId} на {dmg} " +
            $"(щит: -{toShield}, HP: -{toHp}){reactionNote}");

        if (defender.Hp <= 0)
        {
            defender.Hp = 0;
            State.LogAdd($"X {defender.DefId} пал!");
            var side = FindSide(defender);
            if (side != null && side.Active == defender)
            {
                var next = side.Alive.FirstOrDefault();
                if (next != null) side.ActiveIndex = side.Characters.IndexOf(next);
            }
            CheckGameOver();
        }
    }

    private PlayerSide? FindSide(CharacterState c) =>
        State.SideA.Characters.Contains(c) ? State.SideA :
        State.SideB.Characters.Contains(c) ? State.SideB : null;

    private void CheckGameOver()
    {
        foreach (var side in new[] { State.SideA, State.SideB })
        {
            if (!side.Alive.Any())
            {
                State.Phase = Phase.GameOver;
                State.WinnerId = State.Other(side).PlayerId;
                State.LogAdd($"=== ПОБЕДА: {State.WinnerId} ===");
            }
        }
    }

    // ---------- Энд-фаза ----------

    private void EndRound()
    {
        foreach (var side in new[] { State.SideA, State.SideB })
            foreach (var c in side.Alive)
                c.Energy = Math.Min(c.EnergyMax, c.Energy + EnergyPerRoundTick);

        State.LogAdd($"Конец раунда {State.Round}: +{EnergyPerRoundTick} энергии всем живым");
    }

    private static bool PayAny(PlayerSide side, int amount)
    {
        if (side.Dice.Dice.Count < amount) return false;
        for (int i = 0; i < amount; i++) side.Dice.Dice.RemoveAt(0);
        return true;
    }
}
