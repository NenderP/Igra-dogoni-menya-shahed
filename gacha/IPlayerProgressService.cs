namespace Gacha;

/// <summary>
/// Награды за бой — генерирует сервер боя, зачисляет гача-модуль
/// (protocol-v0.md, решение №2). Гача владеет dust/currency, сервер только дергает интерфейс.
/// </summary>
public record BattleRewards(int Dust, int Currency);

public interface IPlayerProgressService
{
    /// <summary>Зачислить награду игроку. Возвращает новые балансы (dust, currency).</summary>
    (int Dust, int Currency) AddRewards(string playerId, BattleRewards rewards);
}

/// <summary>In-memory реализация v0 поверх AccountService. Позже — БД/файл.</summary>
public class InMemoryPlayerProgressService : IPlayerProgressService
{
    private readonly AccountService _accounts;

    public InMemoryPlayerProgressService(AccountService accounts) => _accounts = accounts;

    public (int Dust, int Currency) AddRewards(string playerId, BattleRewards rewards)
    {
        var acc = _accounts.Get(playerId)
            ?? throw new InvalidOperationException($"Аккаунт {playerId} не найден — сначала hello/getOrCreate");

        acc.Collection.Dust += rewards.Dust;
        acc.Collection.Currency += rewards.Currency;
        return (acc.Collection.Dust, acc.Collection.Currency);
    }
}
