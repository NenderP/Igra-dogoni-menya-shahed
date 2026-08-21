# /gacha — гача, аккаунты, коллекция, боты

Зона: агент друга NenderP (@kinder-joy228).

## Что здесь живёт

- **Гача-движок**: шансы `0.6% / 5.1% / 94.3%`, хард-пити 90, софт-пити с 75, гарант 50/50 (`GachaSystem.cs:10`, `GachaConfig.cs:10`)
- **Пити**: счётчики `pulls_since_5star`, флаг `guaranteed_featured` — сервер хранит на аккаунте (`PlayerCollection.cs:15`)
- **Коллекция**: владение копиями, `is_new` vs дубли (`PlayerCollection.cs:30`)
- **Пыль**: дубли → пыль 5/25/100, крафт `dust_to_pulls` 60 пыли = 1 крутка (диапазон 50–75 из README, дефолт 60) (`DustSystem.cs:10`)
- **Аккаунты**: `player_id` без пароля в v1, `display_name` + локальное хранение (`Account.cs:10`)
- **Боты**: генератор рандомных колод 3 уровня (`BotDeckGenerator.cs:10`) — easy/normal/hard, без дублей в отряде
- **Контент-справочник**: `def_id` персонажей публикует этот модуль в `/shared/content.md:10`

## Принципы

- Все роллы и пити — server-authoritative, детерминированные, без клиента
- RNG — один инстанс `Random` на сервер, сидируется для тестов
- Справочник `/shared/content.md` — единый источник `def_id` для `/server` и `/client`
- Сообщения — по `shared/protocol-v0.md:91` (`gacha_pull` → `gacha_result`, `collection_sync` → `collection_state`, `dust_to_pulls`)
- Награды за бой (`game_over.rewards`) генерит `/server`, зачисляет этот модуль через `IPlayerProgressService.AddRewards` (см. Открытые вопросы №2)

## Статус

- ✅ Задача №4 — каркас гачи реализован (шансы/пити/пыль)
- ✅ Задача №5 — генератор колод ботов реализован
- 🔶 Ждёт ревью протокола v0 вместе с @ox-alpha, затем интеграция с сервером боя

## Быстрый старт (когда появится .NET SDK)

```bash
# тест шансов (симуляция 100k круток)
dotnet run --project gacha -- simulate --pulls 100000
# генерация колод ботов
dotnet run --project gacha -- bot-decks --difficulty hard --count 5
```

## Файлы

| Файл | Назначение |
|---|---|
| `GachaConfig.cs` | Константы шансов, пити, пыли |
| `GachaBanner.cs` | Пул баннера, фичер 5★, 50/50 |
| `GachaSystem.cs` | Логика `Pull(count)` с софт/хард пити |
| `PlayerCollection.cs` | Хранение коллекции, пити-счётчиков, пыли |
| `DustSystem.cs` | Конвертация дублей и `DustToPulls` |
| `BotDeckGenerator.cs` | 3 уровня сложности, рандомные колоды |
| `Account.cs` | Аккаунт без пароля (v1) |
