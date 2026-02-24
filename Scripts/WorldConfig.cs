using Godot;

namespace VoidQuest;

/// <summary>
/// Глобальная конфигурация мира. Создай ресурс этого типа в инспекторе
/// и назначь его полю Config у WorldGenerator.
/// </summary>
[GlobalClass]
public partial class WorldConfig : Resource
{
    // ─────────────────────────────────────────────────────────
    // ВОДА
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Water")]

    /// <summary>Нормализованный уровень моря [0..1]. Всё ниже = океан.</summary>
    [Export(PropertyHint.Range, "0.1,0.9,0.01")]
    public float SeaLevel { get; set; } = 0.38f;

    [Export] public Color WaterShallowColor { get; set; } = new Color(0.18f, 0.53f, 0.84f);
    [Export] public Color WaterDeepColor    { get; set; } = new Color(0.04f, 0.20f, 0.50f);

    [Export(PropertyHint.Range, "0.0,0.5,0.01")]
    public float WaveStrength { get; set; } = 0.06f;

    [Export(PropertyHint.Range, "0.0,3.0,0.05")]
    public float WaveSpeed { get; set; } = 0.55f;

    // ─────────────────────────────────────────────────────────
    // ФОРМА РЕЛЬЕФА (три слоя как в Minecraft)
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Terrain Shape")]

    /// <summary>Континентальность: очень низкая частота, разделяет океан и сушу.</summary>
    [Export(PropertyHint.Range, "0.0001,0.005,0.00005")]
    public float ContinentalFreq { get; set; } = 0.00055f;

    /// <summary>Эрозия: определяет, насколько плоская суша (высокая = равнины, низкая = горы).</summary>
    [Export(PropertyHint.Range, "0.0005,0.02,0.0001")]
    public float ErosionFreq { get; set; } = 0.0028f;

    /// <summary>Пики/хребты (ridged FBM): добавляет острые горные хребты.</summary>
    [Export(PropertyHint.Range, "0.001,0.05,0.0005")]
    public float PeaksFreq { get; set; } = 0.007f;

    [Export(PropertyHint.Range, "1,6")] public int ContinentalOctaves { get; set; } = 3;
    [Export(PropertyHint.Range, "1,8")] public int ErosionOctaves     { get; set; } = 4;
    [Export(PropertyHint.Range, "1,8")] public int PeaksOctaves       { get; set; } = 5;

    // ─────────────────────────────────────────────────────────
    // КЛИМАТ
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Climate")]

    [Export(PropertyHint.Range, "0.0001,0.005,0.00005")]
    public float TemperatureFreq { get; set; } = 0.0007f;

    [Export(PropertyHint.Range, "0.0001,0.005,0.00005")]
    public float HumidityFreq { get; set; } = 0.0009f;

    /// <summary>Насколько широта (Z-координата) охлаждает климат.</summary>
    [Export(PropertyHint.Range, "0.0,0.001,0.00001")]
    public float LatitudeTempScale { get; set; } = 0.00013f;

    // ─────────────────────────────────────────────────────────
    // РЕКИ
    // ─────────────────────────────────────────────────────────
    [ExportGroup("Rivers")]

    [Export] public bool EnableRivers { get; set; } = true;

    [Export(PropertyHint.Range, "0.0005,0.01,0.0001")]
    public float RiverFreq { get; set; } = 0.0014f;

    /// <summary>Ширина реки: меньше = уже. Ridged noise threshold.</summary>
    [Export(PropertyHint.Range, "0.01,0.25,0.005")]
    public float RiverThreshold { get; set; } = 0.07f;

    /// <summary>Реки прорезаются только ниже этой высоты над уровнем моря (нормализованная).</summary>
    [Export(PropertyHint.Range, "0.0,0.5,0.01")]
    public float MaxRiverAltitude { get; set; } = 0.20f;
}
