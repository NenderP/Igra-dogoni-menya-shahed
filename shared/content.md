# Контент — единый справочник def_id (v1)

> Владелец: модуль `/gacha` (@kinder-joy228). Сервер боя и клиент ссылаются на эти `def_id`. Менять только через WAR_ROOM.
> Источник правды для `protocol-v0.md` полей `def_id`, `rarity`, `characters`.

## Персонажи v1 (8 штук — 2×5★, 4×4★, 2×3★)

| def_id | Редкость | Время суток | Имя (рабочее) | Роль |
|---|---|---|---|---|
| `char_eclipse_sovereign` | 5★ | `eclipse` | Властелин Затмения | Фичер легендарка баннера, тёмный урон / риск |
| `char_dawn_herald` | 5★ | `dawn` | Вестник Рассвета | Легендарка, хилы/усиления |
| `char_day_mage` | 4★ | `day` | Маг Полдня | Прямой урон |
| `char_night_assassin` | 4★ | `night` | Ночной Ассасин | Криты/энергия |
| `char_twilight_trickster` | 4★ | `twilight` | Сумеречный Трикстер | Хитрость/комбо |
| `char_dusk_scout` | 4★ | `twilight` | Сумеречный Разведчик | Поддержка |
| `char_day_squire` | 3★ | `day` | Оруженосец Дня | Стартовый |
| `char_night_initiate` | 3★ | `night` | Посвящённый Ночи | Стартовый |

Пул баннера v1: все 8 в общем пуле. Фичер: `char_eclipse_sovereign` с механикой 50/50 (при проигрыше — следующий 5★ гарант фичера). Второй 5★ (`char_dawn_herald`) выпадает только при проигрыше 50/50 (как не-фичер).

Будущие баннеры: меняется `featured_5star_def_id` в `GachaBanner.cs:10`.

### Базовые статы (TBD, баланс — плейтесты)

| def_id | HP | Энергия ульты | Стихия |
|---|---|---|---|
| `char_eclipse_sovereign` | 12 | 3 | eclipse |
| `char_dawn_herald` | 11 | 3 | dawn |
| `char_day_mage` | 10 | 2 | day |
| `char_night_assassin` | 10 | 2 | night |
| `char_twilight_trickster` | 9 | 2 | twilight |
| `char_dusk_scout` | 9 | 2 | twilight |
| `char_day_squire` | 8 | 2 | day |
| `char_night_initiate` | 8 | 2 | night |

Стоимость скиллов / урон — см. `docs/battle-system.md:40`.

## Карты поддержки (стартовый набор, расширяется)

| def_id | Эффект (кратко) |
|---|---|
| `sup_shield_1` | Щит 2 HP на активного |
| `sup_double_dice` | Переброс 2 дайсов бесплатно |
| `sup_heal_2` | Хил 2 HP |
| `sup_energy_boost` | +1 энергии активному |
| `sup_dice_fix` | Заменить дайс на `omni` |

Выдача: по 1 в начале каждого раунда (`docs/battle-system.md:29`). Лимит на поле — TBD людьми.

## Связь с протоколом

- `gacha_result.items[].def_id` ∈ этом файле
- `state_sync.you.characters[].def_id` и `opponent.characters[].def_id` ∈ этом файле
- `BotDeck.characters` ∈ этом файле (3 штуки, без дублей в отряде)
