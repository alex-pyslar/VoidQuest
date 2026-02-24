using Godot;
using Godot.Collections;

namespace VoidQuest;

/// <summary>
/// Описание одного биома. Биом выбирается по трём осям:
///   • Height     — нормализованная высота [0..1]
///   • Temperature — температура [0..1]  (0=холодно, 1=жарко)
///   • Humidity    — влажность [0..1]    (0=сухо, 1=влажно)
///
/// Пример стандартных биомов:
///   Океан    : H 0.00–0.38, T любая, W любая,  IsOcean=true
///   Пляж     : H 0.38–0.42, T любая, W любая
///   Пустыня  : H 0.42–0.65, T 0.65–1.0, W 0.0–0.35
///   Саванна  : H 0.42–0.65, T 0.55–1.0, W 0.35–0.60
///   Равнины  : H 0.42–0.65, T 0.30–0.70, W 0.35–0.70
///   Лес      : H 0.42–0.70, T 0.30–0.70, W 0.60–1.0
///   Тайга    : H 0.42–0.70, T 0.10–0.35, W 0.40–1.0
///   Тундра   : H 0.42–0.65, T 0.0–0.20,  W любая
///   Горы     : H 0.65–0.85, T любая, W любая
///   Снег     : H 0.85–1.0,  T любая, W любая
/// </summary>
[GlobalClass]
public partial class BiomeData : Resource
{
    [Export] public string Name { get; set; } = "Biome";

    // ─────────────────────────────────────────────────────────
    // УСЛОВИЯ СПАВНА (все три оси должны совпасть)
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Spawn Conditions")]

    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float MinHeight   { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float MaxHeight   { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float MinTemp     { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float MaxTemp     { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float MinHumidity { get; set; } = 0.0f;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float MaxHumidity { get; set; } = 1.0f;

    // ─────────────────────────────────────────────────────────
    // МОДИФИКАТОРЫ РЕЛЬЕФА
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Terrain Modifiers")]

    /// <summary>Умножитель высоты (>1 поднимает, &lt;1 опускает).</summary>
    [Export] public float HeightMul     { get; set; } = 1.0f;

    /// <summary>Дополнительный горный буст (0 = без изменений).</summary>
    [Export] public float MountainBoost { get; set; } = 0.0f;

    /// <summary>Пометить как водный биом (объекты не спавнятся).</summary>
    [Export] public bool  IsOcean       { get; set; } = false;

    // ─────────────────────────────────────────────────────────
    // ВИЗУАЛ
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Visuals")]

    /// <summary>Текстура для triplanar-проекции. Если не задана — используется процедурная окраска.</summary>
    [Export] public Texture2D Texture { get; set; }

    // ─────────────────────────────────────────────────────────
    // ОБЪЕКТЫ
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Objects")]

    [Export] public Array<PackedScene> Objects { get; set; } = new Array<PackedScene>();

    [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float ObjectChance { get; set; } = 0.2f;
}
