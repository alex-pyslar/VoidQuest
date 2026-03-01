using Godot;
using System.Collections.Generic;

namespace VoidQuest;

/// <summary>
/// Autoload singleton: EN / RU translation.
/// Item names and descriptions are stored on ItemDef (loaded from items.json),
/// so only UI strings need to live here.
/// Usage: LocaleManager.Instance.T("key")
///        LocaleManager.Instance.ItemName(itemId)
///        LocaleManager.Instance.ItemDesc(itemId)
/// </summary>
public partial class LocaleManager : Node
{
    public static LocaleManager Instance { get; private set; }

    private static string _lang = "ru";   // persists across scene changes
    public string Lang => _lang;

    [Signal] public delegate void LangChangedEventHandler();

    // ── English ───────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> _en = new()
    {
        // Main menu
        ["menu.play"]        = "PLAY",
        ["menu.multiplayer"] = "MULTIPLAYER",
        ["menu.settings"]    = "SETTINGS",
        ["menu.quit"]        = "QUIT",
        ["menu.title"]       = "VOID QUEST",
        ["menu.lang"]        = "LANGUAGE",
        ["menu.lang.en"]     = "EN",
        ["menu.lang.ru"]     = "RU",
        ["menu.back"]        = "BACK",

        // Lobby
        ["lobby.title"]      = "LOBBY",
        ["lobby.host"]       = "HOST GAME",
        ["lobby.join"]       = "JOIN GAME",
        ["lobby.start"]      = "START",
        ["lobby.back"]       = "BACK",
        ["lobby.ip"]         = "Server IP:",
        ["lobby.connect"]    = "CONNECT",
        ["lobby.players"]    = "PLAYERS:",
        ["lobby.your_ip"]    = "Your IP:",
        ["lobby.waiting"]    = "Waiting for players...",
        ["lobby.connecting"] = "Connecting...",

        // HUD — stat labels
        ["hud.score"]        = "Score:",
        ["hud.level"]        = "Lv",
        ["hud.coords"]       = "Pos:",
        ["hud.enemies"]      = "Enemies:",
        ["hud.hp"]           = "HP",
        ["hud.mp"]           = "MP",

        // Inventory / Equipment
        ["hud.inventory"]    = "INVENTORY",
        ["hud.crafting"]     = "CRAFTING",
        ["hud.equipment"]    = "EQUIPMENT",
        ["hud.info_title"]   = "ITEM INFO",
        ["hud.char"]         = "CHARACTER",
        ["hud.backpack"]     = "BACKPACK",
        ["hud.stats"]        = "STATS",
        ["hud.abilities"]    = "ACTIVE ABILITIES",
        ["hud.weapon"]       = "Weapon",
        ["hud.armor"]        = "Armor",
        ["hud.shield"]       = "Shield",
        ["hud.ring"]         = "Ring",
        ["hud.amulet"]       = "Amulet",
        ["hud.none"]         = "None",
        ["hud.unequip"]      = "UNEQUIP",
        ["hud.craft"]        = "CRAFT",
        ["hud.use"]          = "USE",
        ["hud.equip"]        = "EQUIP",
        ["hud.equipped"]     = "(equipped)",
        ["hud.drop"]         = "DROP",
        ["hud.empty"]        = "— empty —",
        ["hud.item_info"]    = "Select an item\nor equip slot…",
        ["hud.ability_hint"] = "[4] Lightning  [5] Void Burst",

        // Spell names (hotbar)
        ["spell.fireball"]         = "FIRE",
        ["spell.heal"]             = "HEAL",
        ["spell.frost_nova"]       = "FROST",
        ["spell.lightning_strike"] = "BOLT",
        ["spell.void_burst"]       = "VOID",
        ["hud.spells"]             = "SPELLS",
        ["hud.spell_locked"]       = "locked",
        ["hud.spell_ready"]        = "ready",

        // Pause
        ["pause.title"]      = "PAUSED",
        ["pause.resume"]     = "RESUME",
        ["pause.menu"]       = "MAIN MENU",

        // Game over
        ["gameover.title"]   = "YOU DIED",
        ["gameover.score"]   = "Score:",
        ["gameover.restart"] = "RESTART",
        ["gameover.menu"]    = "MAIN MENU",

        // Stats labels
        ["stat.atk"]         = "ATK",
        ["stat.def"]         = "DEF",
        ["stat.mag"]         = "MAG",
        ["stat.lv"]          = "Lv",
        ["stat.hp"]          = "HP",
        ["stat.mp"]          = "MP",
        ["stat.xp"]          = "XP",

        // Crafting
        ["craft.hint"]       = "Gather materials from enemies & chests.\nCraft potions, rings and powerful amulets.",

        // Abilities (full description shown in item tooltip)
        ["ability.fireball_boost"]         = "Enhanced Fireball: Fireball deals +50% damage.",
        ["ability.fireball_boost.short"]   = "Enhanced Fireball",
        ["ability.frost_boost"]            = "Enhanced Frost Nova: +5 m radius, +2 s freeze.",
        ["ability.frost_boost.short"]      = "Enhanced Frost Nova",
        ["ability.life_regen"]             = "Life Regeneration: +3 HP every 2 seconds.",
        ["ability.life_regen.short"]       = "Life Regeneration",
        ["ability.blood_drain"]            = "Blood Drain: Melee heals 15% of damage dealt.",
        ["ability.blood_drain.short"]      = "Blood Drain",
        ["ability.lightning_strike"]       = "Lightning Strike [4]: Damages all enemies in 20 m. 35 MP / 6 s CD.",
        ["ability.lightning_strike.short"] = "Lightning Strike [4]",
        ["ability.void_burst"]             = "Void Burst [5]: Explosion in 10 m radius. 50 MP / 10 s CD.",
        ["ability.void_burst.short"]       = "Void Burst [5]",
    };

    // ── Russian ───────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> _ru = new()
    {
        // Main menu
        ["menu.play"]        = "ИГРАТЬ",
        ["menu.multiplayer"] = "МУЛЬТИПЛЕЕР",
        ["menu.settings"]    = "НАСТРОЙКИ",
        ["menu.quit"]        = "ВЫХОД",
        ["menu.title"]       = "VOID QUEST",
        ["menu.lang"]        = "ЯЗЫК",
        ["menu.lang.en"]     = "EN",
        ["menu.lang.ru"]     = "RU",
        ["menu.back"]        = "НАЗАД",

        // Lobby
        ["lobby.title"]      = "ЛОББИ",
        ["lobby.host"]       = "СОЗДАТЬ ИГРУ",
        ["lobby.join"]       = "ПОДКЛЮЧИТЬСЯ",
        ["lobby.start"]      = "СТАРТ",
        ["lobby.back"]       = "НАЗАД",
        ["lobby.ip"]         = "IP сервера:",
        ["lobby.connect"]    = "ПОДКЛЮЧИТЬСЯ",
        ["lobby.players"]    = "ИГРОКИ:",
        ["lobby.your_ip"]    = "Ваш IP:",
        ["lobby.waiting"]    = "Ожидание игроков...",
        ["lobby.connecting"] = "Подключение...",

        // HUD — stat labels
        ["hud.score"]        = "Очки:",
        ["hud.level"]        = "Ур.",
        ["hud.coords"]       = "Позиция:",
        ["hud.enemies"]      = "Враги:",
        ["hud.hp"]           = "ОЗ",
        ["hud.mp"]           = "МН",

        // Inventory / Equipment
        ["hud.inventory"]    = "ИНВЕНТАРЬ",
        ["hud.crafting"]     = "КРАФТ",
        ["hud.equipment"]    = "СНАРЯЖЕНИЕ",
        ["hud.info_title"]   = "О ПРЕДМЕТЕ",
        ["hud.char"]         = "ПЕРСОНАЖ",
        ["hud.backpack"]     = "РЮКЗАК",
        ["hud.stats"]        = "ХАРАКТЕРИСТИКИ",
        ["hud.abilities"]    = "АКТИВНЫЕ СПОСОБНОСТИ",
        ["hud.weapon"]       = "Оружие",
        ["hud.armor"]        = "Броня",
        ["hud.shield"]       = "Щит",
        ["hud.ring"]         = "Кольцо",
        ["hud.amulet"]       = "Амулет",
        ["hud.none"]         = "Нет",
        ["hud.unequip"]      = "СНЯТЬ",
        ["hud.craft"]        = "СОЗДАТЬ",
        ["hud.use"]          = "ПРИМЕНИТЬ",
        ["hud.equip"]        = "НАДЕТЬ",
        ["hud.equipped"]     = "(надето)",
        ["hud.drop"]         = "ВЫБРОСИТЬ",
        ["hud.empty"]        = "— пусто —",
        ["hud.item_info"]    = "Выберите предмет\nили слот снаряжения…",
        ["hud.ability_hint"] = "[4] Молния  [5] Взрыв пустоты",

        // Spell names (hotbar)
        ["spell.fireball"]         = "ОГОНЬ",
        ["spell.heal"]             = "ЛЕЧЕНИЕ",
        ["spell.frost_nova"]       = "МОРОЗ",
        ["spell.lightning_strike"] = "МОЛНИЯ",
        ["spell.void_burst"]       = "ПУСТОТА",
        ["hud.spells"]             = "ЗАКЛИНАНИЯ",
        ["hud.spell_locked"]       = "заблок.",
        ["hud.spell_ready"]        = "готово",

        // Pause
        ["pause.title"]      = "ПАУЗА",
        ["pause.resume"]     = "ПРОДОЛЖИТЬ",
        ["pause.menu"]       = "ГЛАВНОЕ МЕНЮ",

        // Game over
        ["gameover.title"]   = "ВЫ ПОГИБЛИ",
        ["gameover.score"]   = "Очки:",
        ["gameover.restart"] = "ЗАНОВО",
        ["gameover.menu"]    = "ГЛАВНОЕ МЕНЮ",

        // Stats labels
        ["stat.atk"]         = "АТК",
        ["stat.def"]         = "ЗАЩ",
        ["stat.mag"]         = "МАГ",
        ["stat.lv"]          = "Ур",
        ["stat.hp"]          = "ОЗ",
        ["stat.mp"]          = "МН",
        ["stat.xp"]          = "ОП",

        // Crafting
        ["craft.hint"]       = "Собирайте материалы с врагов и сундуков.\nКрафтите зелья, кольца и мощные амулеты.",

        // Abilities
        ["ability.fireball_boost"]         = "Усиленный Огненный шар: +50% урона.",
        ["ability.fireball_boost.short"]   = "Усиленный Огненный шар",
        ["ability.frost_boost"]            = "Усиленная Ледяная нова: +5 м радиус, +2 с заморозки.",
        ["ability.frost_boost.short"]      = "Усиленная Ледяная нова",
        ["ability.life_regen"]             = "Регенерация жизни: +3 ОЗ каждые 2 секунды.",
        ["ability.life_regen.short"]       = "Регенерация жизни",
        ["ability.blood_drain"]            = "Кровопийца: ближний бой восстанавливает 15% урона.",
        ["ability.blood_drain.short"]      = "Кровопийца",
        ["ability.lightning_strike"]       = "Удар молнии [4]: поражает всех врагов в 20 м. 35 МН / КД 6 с.",
        ["ability.lightning_strike.short"] = "Удар молнии [4]",
        ["ability.void_burst"]             = "Взрыв пустоты [5]: взрыв в радиусе 10 м. 50 МН / КД 10 с.",
        ["ability.void_burst.short"]       = "Взрыв пустоты [5]",
    };

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Translate a UI key into the current language. Returns the key itself if not found.</summary>
    public string T(string key)
    {
        var dict = _lang == "ru" ? _ru : _en;
        return dict.TryGetValue(key, out var val) ? val : key;
    }

    /// <summary>
    /// Returns the localised display name for an item id.
    /// Reads NameRu / Name directly from ItemDef (loaded from items.json).
    /// Falls back to the raw id if the item is unknown.
    /// </summary>
    public string ItemName(string itemId)
    {
        var def = ItemDatabase.Get(itemId);
        if (def == null) return itemId;
        return _lang == "ru" && !string.IsNullOrEmpty(def.NameRu) ? def.NameRu : def.Name;
    }

    /// <summary>
    /// Returns the localised description for an item id.
    /// Reads DescRu / Description directly from ItemDef (loaded from items.json).
    /// </summary>
    public string ItemDesc(string itemId)
    {
        var def = ItemDatabase.Get(itemId);
        if (def == null) return "";
        return _lang == "ru" && !string.IsNullOrEmpty(def.DescRu) ? def.DescRu : def.Description;
    }

    public void SetLang(string lang)
    {
        if (_lang == lang) return;
        _lang = lang;
        EmitSignal(SignalName.LangChanged);
    }

    public void Toggle() => SetLang(_lang == "ru" ? "en" : "ru");
}
