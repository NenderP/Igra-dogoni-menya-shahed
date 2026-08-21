using Gacha;

namespace Server.Net;

/// <summary>
/// Гача-слой сервера: аккаунты, крутки, коллекция, пыль, награды за бой.
/// Все роллы — здесь (server-authoritative).
/// </summary>
public class GachaService
{
    private readonly AccountService _accounts = new();
    private readonly GachaSystem _gacha = new();
    private readonly IPlayerProgressService _progress;

    public GachaService() => _progress = new InMemoryPlayerProgressService(_accounts);

    public Account GetOrCreate(string playerId, string displayName) =>
        _accounts.GetOrCreate(playerId, displayName);

    public object Pull(string playerId, int count)
    {
        var acc = Require(playerId);
        var result = _gacha.Pull(acc.Collection, count);
        return new
        {
            items = result.Items.Select(i => new
            {
                def_id = i.DefId,
                rarity = i.Rarity,
                is_new = i.IsNew,
                converted_to_dust = i.ConvertedToDust
            }),
            pity_after = new
            {
                pulls_since_5star = result.PityAfter.PullsSince5Star,
                guaranteed_featured = result.PityAfter.GuaranteedFeatured
            },
            dust_balance = result.DustBalance,
            currency_spent = 0 // v0: крутки пока бесплатные, валюту введём с клиентом
        };
    }

    public object CollectionState(string playerId)
    {
        var st = Require(playerId).Collection.ToState();
        return new
        {
            owned = st.Owned.Select(o => new { def_id = o.DefId, copies = o.Copies }),
            dust = st.Dust,
            pity = new
            {
                pulls_since_5star = st.Pity.PullsSince5Star,
                guaranteed_featured = st.Pity.GuaranteedFeatured
            }
        };
    }

    public object DustToPulls(string playerId, int pullsRequested)
    {
        var acc = Require(playerId);
        int given = DustSystem.DustToPulls(acc.Collection, pullsRequested);
        return new { pulls_granted = given, dust_balance = acc.Collection.Dust };
    }

    /// <summary>Начислить награду за бой (вызывает GameSession при game_over).</summary>
    public void AddBattleRewards(string playerId, bool win) =>
        _progress.AddRewards(playerId, new BattleRewards(
            win ? GachaConfig.RewardDustWin : GachaConfig.RewardDustLose, 0));

    private Account Require(string playerId) =>
        _accounts.Get(playerId) ?? throw new InvalidOperationException($"Аккаунт {playerId} не найден");
}
