namespace Gacha;

/// <summary>
/// Состояние коллекции игрока — синхронизируется через collection_state (protocol-v0.md:111).
/// Хранит пити-счётчики server-side.
/// </summary>
public class PlayerCollection
{
    // owned: def_id -> copies
    public Dictionary<string, int> Owned { get; } = new();

    // Валюта/пыль
    public int Dust { get; set; } = 0;
    public int Pulls { get; set; } = 100; // стартовые бесплатные крутки; пополняются пылью
    public int Currency { get; set; } = 0; // премиум-валюта для круток (если нужна)

    // Пити (protocol: pity.pulls_since_5star, guaranteed_featured)
    public int PullsSince5Star { get; set; } = 0;
    public int PullsSince4Star { get; set; } = 0;
    public bool GuaranteedFeatured { get; set; } = false;

    // История для отладки/аналитики
    public int TotalPulls { get; set; } = 0;

    public bool Owns(string defId) => Owned.ContainsKey(defId);

    public int GetCopies(string defId) => Owned.TryGetValue(defId, out var c) ? c : 0;

    public void AddCopy(string defId)
    {
        Owned[defId] = GetCopies(defId) + 1;
    }

    public CollectionState ToState()
    {
        var list = new List<OwnedEntry>();
        foreach (var kv in Owned) list.Add(new OwnedEntry(kv.Key, kv.Value));
        return new CollectionState(list, Dust, Pulls, new PityState(PullsSince5Star, GuaranteedFeatured));
    }
}

public record OwnedEntry(string DefId, int Copies);
public record PityState(int PullsSince5Star, bool GuaranteedFeatured);
public record CollectionState(List<OwnedEntry> Owned, int Dust, int Pulls, PityState Pity);
