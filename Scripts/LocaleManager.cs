using Godot;
using System.Collections.Generic;

namespace VoidQuest;

/// <summary>
/// Autoload singleton: EN / RU translation.
/// Usage: LocaleManager.Instance.T("key")
/// </summary>
public partial class LocaleManager : Node
{
    public static LocaleManager Instance { get; private set; }

    private static string _lang = "ru";   // persists across scene changes
    public string Lang => _lang;

    [Signal] public delegate void LangChangedEventHandler();

    // ── Dictionaries ──────────────────────────────────────────────────
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
        ["hud.weapon"]       = "Weapon",
        ["hud.armor"]        = "Armor",
        ["hud.shield"]       = "Shield",
        ["hud.ring"]         = "Ring",
        ["hud.none"]         = "None",
        ["hud.unequip"]      = "UNEQUIP",
        ["hud.craft"]        = "CRAFT",
        ["hud.use"]          = "USE",
        ["hud.equip"]        = "EQUIP",
        ["hud.equipped"]     = "(equipped)",
        ["hud.drop"]         = "DROP",
        ["hud.empty"]        = "— empty —",
        ["hud.item_info"]    = "Select an item\nor equip slot…",

        // Pause
        ["pause.title"]      = "PAUSED",
        ["pause.resume"]     = "RESUME",
        ["pause.menu"]       = "MAIN MENU",

        // Game over
        ["gameover.title"]   = "YOU DIED",
        ["gameover.score"]   = "Score:",
        ["gameover.restart"] = "RESTART",
        ["gameover.menu"]    = "MAIN MENU",
    };

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
        ["hud.weapon"]       = "Оружие",
        ["hud.armor"]        = "Броня",
        ["hud.shield"]       = "Щит",
        ["hud.ring"]         = "Кольцо",
        ["hud.none"]         = "Нет",
        ["hud.unequip"]      = "СНЯТЬ",
        ["hud.craft"]        = "СОЗДАТЬ",
        ["hud.use"]          = "ПРИМЕНИТЬ",
        ["hud.equip"]        = "НАДЕТЬ",
        ["hud.equipped"]     = "(надето)",
        ["hud.drop"]         = "ВЫБРОСИТЬ",
        ["hud.empty"]        = "— пусто —",
        ["hud.item_info"]    = "Выберите предмет\nили слот снаряжения…",

        // Pause
        ["pause.title"]      = "ПАУЗА",
        ["pause.resume"]     = "ПРОДОЛЖИТЬ",
        ["pause.menu"]       = "ГЛАВНОЕ МЕНЮ",

        // Game over
        ["gameover.title"]   = "ВЫ ПОГИБЛИ",
        ["gameover.score"]   = "Очки:",
        ["gameover.restart"] = "ЗАНОВО",
        ["gameover.menu"]    = "ГЛАВНОЕ МЕНЮ",
    };

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Translate a key into the current language.</summary>
    public string T(string key)
    {
        var dict = _lang == "ru" ? _ru : _en;
        return dict.TryGetValue(key, out var val) ? val : key;
    }

    public void SetLang(string lang)
    {
        if (_lang == lang) return;
        _lang = lang;
        EmitSignal(SignalName.LangChanged);
    }

    public void Toggle() => SetLang(_lang == "ru" ? "en" : "ru");
}
