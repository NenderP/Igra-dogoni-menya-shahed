# Протокол v0 (УТВЕРЖДЁН)

> Статус: апрув обеих сторон 2026.08.22 (см. WAR_ROOM). Заморожен до v1.
> Владелец: @ox-alpha, ревьюер: @kinder-joy228.
> Изменения только через договорённость в WAR_ROOM.md.

## Транспорт

- WebSocket (wss), текстовые фреймы, кодировка UTF-8
- Формат конверта у всех сообщений:

```json
{ "type": "имя_сообщения", "payload": { ... } }
```

- Ошибки: `{ "type": "error", "payload": { "code": "bad_request", "message": "..." } }`

## Идентификация

```json
// C → S
{ "type": "hello", "payload": { "player_id": "uuid", "display_name": "NenderP" } }

// S → C
{ "type": "welcome", "payload": { "player_id": "uuid", "dust": 120, "collection_size": 14 } }
```

`player_id` выдаётся сервером при первом hello и сохраняется клиентом локально (аккаунты без пароля в v1 — обсуждаем).

## Матчмейкинг

```json
// C → S: три способа начать игру
{ "type": "vs_bot",   "payload": { "difficulty": "easy|normal|hard" } }
{ "type": "create_lobby", "payload": {} }                      // вернёт код
{ "type": "join_lobby",   "payload": { "code": "AB12" } }
{ "type": "find_match",   "payload": {} }                      // пул рандома

// S → C
{ "type": "match_found", "payload": { "mode": "bot|duel", "opponent": { "id": "...", "name": "..." }, "you_go_first": true } }
```

## Бой

Сервер после match_found присылает полное состояние:

```json
// S → C
{ "type": "state_sync", "payload": {
    "round": 1,
    "you": { "characters": [ { "uid": "c1", "def_id": "char_dawn_knight", "hp": 10, "energy": 0, "status": [] } ],
             "hand": ["sup_shield_1", "sup_double_dice"],
             "dice": ["dawn","day","omni","night","day","twilight","eclipse","day"],
             "supports_on_field": [], "rerolls_left": 1 },
    "opponent": { "characters": [ ... ], "supports_on_field": [], "hand_count": 4 },
    "active_character": "c1"
} }
```

Фазы раунда (сервер ведёт сам, клиент рисует):

```json
// S → C: начало раунда — раздача карт поддержки
{ "type": "round_start", "payload": { "round": 2, "support_cards_drawn": ["sup_heal_2"] } }

// S → C: бросок дайсов (server-authoritative)
{ "type": "dice_rolled", "payload": { "you": [...8 граней...], "opponent_hidden": true } }

// C → S: переброс части дайсов (как в Геншине, до N штук)
{ "type": "reroll_dice", "payload": { "indexes": [2, 5] } }

// C → S: действия игрока
{ "type": "play_card",     "payload": { "card_uid": "sup_heal_2", "target": "c1" } }
{ "type": "use_skill",     "payload": { "character_uid": "c1", "pay_dice": ["day","omni"], "target_uid": "e2" } }
{ "type": "use_ultimate",  "payload": { "character_uid": "c1", "target_uid": null } }
{ "type": "swap_character","payload": { "to_uid": "c3" } }
{ "type": "end_turn",      "payload": {} }

// S → C: результат любого действия (в т.ч. реакции)
{ "type": "action_result", "payload": {
    "actor": "you",
    "action": "use_skill",
    "damage": [{ "target": "e2", "amount": 3, "reaction": "twilight_burst" }],
    "heal":   [{ "target": "c1", "amount": 1 }],
    "log": "День+Ночь → Сумерки: +2 урона"
} }

// S → C: конец игры
{ "type": "game_over", "payload": { "winner": "you|opponent", "rewards": { "dust": 15, "currency": 50 } } }
```

## Гача и коллекция (черновик по запросу @kinder-joy228)

```json
// C → S: крутка (1 или 10)
{ "type": "gacha_pull", "payload": { "count": 10 } }

// S → C: результат крутки
{ "type": "gacha_result", "payload": {
    "items": [
      { "def_id": "char_night_assassin", "rarity": 5, "is_new": false, "converted_to_dust": 100 },
      { "def_id": "char_day_mage",       "rarity": 4, "is_new": true,  "converted_to_dust": 0 }
    ],
    "pity_after": { "pulls_since_5star": 0, "guaranteed_featured": false },
    "dust_balance": 245,
    "currency_spent": 1600
} }

// C → S: синк коллекции (после крутки, входа, крафта пылью)
{ "type": "collection_sync", "payload": {} }
// S → C:
{ "type": "collection_state", "payload": {
    "owned": [ { "def_id": "char_day_mage", "copies": 2 }, { "def_id": "char_dusk_scout", "copies": 1 } ],
    "dust": 245,
    "pity": { "pulls_since_5star": 34, "guaranteed_featured": true }
} }

// C → S: крафт крутки пылью (50–75 пыли = 1 крутка, точная цена TBD)
{ "type": "dust_to_pulls", "payload": { "pulls": 1 } }
```

## Справочники контента

Идентификаторы (`def_id`) персонажей/карт определяет модуль /gacha и публикует в `/shared/content.md`.
Сервер боя ссылается на них же (персонажи в отряде). Единый источник правды — файл, не хардкод.

## Решения по открытым вопросам (зафиксировано 2026.08.22)

1. **Аккаунты v1 без пароля** — принято: `player_id` выдаётся при `hello`, хранится клиентом локально, токен добавим позже без ломки API.
2. **Награды за бой**: генерит сервер боя, зачисляет через `IPlayerProgressService.AddRewards(player_id, rewards)` — интерфейс со стороны /gacha (добавит @kinder-joy228).
3. **`state_sync`** — целиком в v0, дельты позже.
4. **Колода ботов**: `/gacha` отдаёт `BotDeck {difficulty, characters[3], supports[], strategyHint}` через `IBotDeckProvider.GetDeck(difficulty)`. JSON-вид: `{"characters":["char_day_mage",...],"supports":[...],"strategyHint":"..."}` — формат заморожен.
5. **Справочник контента** — `/shared/content.md`, единый источник `def_id`.
