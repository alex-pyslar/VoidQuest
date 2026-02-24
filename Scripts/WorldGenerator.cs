using System;
using System.Collections.Generic;
using Godot;

namespace VoidQuest;

public partial class WorldGenerator : Node3D
{
    // =========================================================
    // EXPORTS
    // =========================================================
    [Export] public int   ChunkSize      { get; set; } = 32;
    [Export] public int   ViewDistance   { get; set; } = 6;
    [Export] public float HeightMultiplier { get; set; } = 48.0f;
    [Export] public float TriplanarScale { get; set; } = 0.15f;

    /// <summary>
    /// Fixed seed for reproducible worlds. 0 = generate a new random seed every run.
    /// The actual seed used is always printed to the console so you can recreate a world.
    /// </summary>
    [Export] public int Seed { get; set; } = 0;

    /// <summary>Сколько чанков генерировать за кадр. Меньше = плавнее, больше = быстрее загрузка.</summary>
    [Export] public int ChunksPerFrame { get; set; } = 2;

    /// <summary>Конфигурация мира. Если не задана — используются значения по умолчанию.</summary>
    [Export] public WorldConfig Config { get; set; }

    [Export] public Godot.Collections.Array<Resource> Biomes { get; set; } = new();

    // =========================================================
    // NOISE — 6 слоёв, как в Minecraft
    // =========================================================
    private FastNoiseLite _continental = new(); // очень низкая частота → форма континентов
    private FastNoiseLite _erosion     = new(); // средняя → плоскость vs горы
    private FastNoiseLite _peaks       = new(); // ridged FBM → острые хребты
    private FastNoiseLite _temperature = new(); // климат
    private FastNoiseLite _humidity    = new(); // влажность
    private FastNoiseLite _river       = new(); // ridged → долины рек

    // =========================================================
    // CHUNKS
    // =========================================================
    private readonly Dictionary<Vector2I, Node3D> _chunks   = new();
    private readonly Queue<Vector2I>               _genQueue = new();
    private readonly HashSet<Vector2I>             _queued   = new();
    private Vector2I _lastCenter = new(-999, -999);

    // =========================================================
    // SHARED RESOURCES (создаются один раз в _Ready)
    // =========================================================
    private ShaderMaterial _terrainMat;
    private ShaderMaterial _waterMat;

    // =========================================================
    // READY
    // =========================================================
    public override void _Ready()
    {
        if (Config == null) Config = new WorldConfig();

        // Use a fixed Seed if set in the Inspector, otherwise generate a new one
        // using .NET Random.Shared which is seeded from OS entropy on startup.
        int seed = Seed != 0 ? Seed : Random.Shared.Next();
        GD.Print($"[World] Seed: {seed}  (set the Seed export to reproduce this world)");

        InitNoise(_continental, seed,     Config.ContinentalFreq, Config.ContinentalOctaves, FastNoiseLite.FractalTypeEnum.Fbm);
        InitNoise(_erosion,     seed + 1, Config.ErosionFreq,     Config.ErosionOctaves,     FastNoiseLite.FractalTypeEnum.Fbm);
        InitNoise(_peaks,       seed + 2, Config.PeaksFreq,       Config.PeaksOctaves,       FastNoiseLite.FractalTypeEnum.Ridged);
        InitNoise(_temperature, seed + 3, Config.TemperatureFreq, 2,                         FastNoiseLite.FractalTypeEnum.Fbm);
        InitNoise(_humidity,    seed + 4, Config.HumidityFreq,    2,                         FastNoiseLite.FractalTypeEnum.Fbm);
        InitNoise(_river,       seed + 5, Config.RiverFreq,       3,                         FastNoiseLite.FractalTypeEnum.Ridged);

        _terrainMat = BuildTerrainMaterial();
        _waterMat   = BuildWaterMaterial();
    }

    private static void InitNoise(FastNoiseLite n, int seed, float freq, int octaves,
                                  FastNoiseLite.FractalTypeEnum fractal)
    {
        n.Seed              = seed;
        n.NoiseType         = FastNoiseLite.NoiseTypeEnum.Simplex;
        n.Frequency         = freq;
        n.FractalType       = fractal;
        n.FractalOctaves    = octaves;
        n.FractalGain       = 0.5f;
        n.FractalLacunarity = 2.0f;
    }

    // =========================================================
    // PROCESS — очередь генерации по ChunksPerFrame штук
    // =========================================================
    public override void _Process(double delta)
    {
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;

        var center = new Vector2I(
            (int)MathF.Floor(cam.GlobalPosition.X / ChunkSize),
            (int)MathF.Floor(cam.GlobalPosition.Z / ChunkSize)
        );

        if (center != _lastCenter)
        {
            _lastCenter = center;
            RefreshChunks(center);
        }

        int generated = 0;
        while (_genQueue.Count > 0 && generated < ChunksPerFrame)
        {
            var coord = _genQueue.Dequeue();
            _queued.Remove(coord);
            if (!_chunks.ContainsKey(coord))
            {
                GenerateChunk(coord);
                generated++;
            }
        }
    }

    // =========================================================
    // УПРАВЛЕНИЕ ОЧЕРЕДЬЮ ЧАНКОВ
    // =========================================================
    private void RefreshChunks(Vector2I center)
    {
        var toRemove = new List<Vector2I>();
        foreach (var key in _chunks.Keys)
            if (key.DistanceTo(center) > ViewDistance + 1)
                toRemove.Add(key);

        foreach (var key in toRemove)
        {
            _chunks[key].QueueFree();
            _chunks.Remove(key);
        }

        var toAdd = new List<Vector2I>();
        for (int x = -ViewDistance; x <= ViewDistance; x++)
        for (int z = -ViewDistance; z <= ViewDistance; z++)
        {
            var coord = center + new Vector2I(x, z);
            if (!_chunks.ContainsKey(coord) && !_queued.Contains(coord))
                toAdd.Add(coord);
        }

        // Ближайшие чанки — первыми
        toAdd.Sort((a, b) => a.DistanceTo(center).CompareTo(b.DistanceTo(center)));
        foreach (var coord in toAdd)
        {
            _genQueue.Enqueue(coord);
            _queued.Add(coord);
        }
    }

    // =========================================================
    // СЭМПЛИРОВАНИЕ РЕЛЬЕФА (Minecraft-style: continental + erosion + peaks)
    // =========================================================

    // Вспомогательная: нормализует шум в [0,1]
    private float N01(FastNoiseLite n, float x, float z) =>
        (n.GetNoise2D(x, z) + 1f) * 0.5f;

    /// <summary>
    /// Возвращает финальную высоту вершины в единицах мира.
    /// Побочно заполняет normH (нормализованная [0,1]), temp, humidity.
    /// </summary>
    private float SampleTerrain(float wx, float wz,
                                out float normH, out float temp, out float humidity)
    {
        float c = N01(_continental, wx, wz); // низкая частота → форма берега
        float e = N01(_erosion,     wx, wz); // 0=горы, 1=равнина
        float p = N01(_peaks,       wx, wz); // ridged → высокое = хребет

        float sea = Config.SeaLevel;
        float h;

        if (c < sea)
        {
            // Дно океана: плавно углубляется от берега к центру
            float depth = (sea - c) / sea;            // 0=берег, 1=глубина
            h = sea * (1f - depth * 0.55f);
        }
        else
        {
            float landT = (c - sea) / (1f - sea);  // 0=берег, 1=глубь суши

            // Эрозия даёт плоские долины (высокая e) или холмы (низкая e)
            float baseH = sea + landT * (1f - sea) * (0.35f + (1f - e) * 0.65f);

            // Пики добавляют острые горы там, где мало эрозии и далеко от берега
            float mountain = p * landT * MathF.Max(0f, 1f - e * 1.15f) * 0.42f;

            h = Mathf.Clamp(baseH + mountain, 0f, 1f);
        }

        // Реки: ridged noise создаёт узкие гребни = центр реки
        if (Config.EnableRivers && h > sea)
        {
            float r      = N01(_river, wx, wz);
            float edge   = MathF.Max(0f, r - (1f - Config.RiverThreshold));
            float factor = edge / Config.RiverThreshold;
            float maxH   = sea + Config.MaxRiverAltitude;

            if (h < maxH && factor > 0f)
                h = Mathf.Lerp(h, sea * 0.97f, factor * 0.88f);
        }

        normH    = h;
        float lat = MathF.Abs(wz) * Config.LatitudeTempScale;
        temp     = Mathf.Clamp(N01(_temperature, wx, wz) - lat, 0f, 1f);
        humidity = N01(_humidity, wx, wz);

        return h * HeightMultiplier;
    }

    // =========================================================
    // ВЫБОР БИОМА (по высоте, температуре, влажности)
    // =========================================================
    private (BiomeData biome, int idx) ResolveBiome(float normH, float temp, float hum)
    {
        if (Biomes.Count == 0) return (null, 0);

        // Проход 1: полное совпадение по всем трём осям
        for (int i = 0; i < Biomes.Count; i++)
        {
            var b = (BiomeData)Biomes[i];
            if (normH >= b.MinHeight && normH < b.MaxHeight
             && temp  >= b.MinTemp   && temp  < b.MaxTemp
             && hum   >= b.MinHumidity && hum < b.MaxHumidity)
                return (b, i);
        }

        // Проход 2: высота + температура (игнорируем влажность)
        for (int i = 0; i < Biomes.Count; i++)
        {
            var b = (BiomeData)Biomes[i];
            if (normH >= b.MinHeight && normH < b.MaxHeight
             && temp  >= b.MinTemp   && temp  < b.MaxTemp)
                return (b, i);
        }

        // Проход 3: только высота
        for (int i = 0; i < Biomes.Count; i++)
        {
            var b = (BiomeData)Biomes[i];
            if (normH >= b.MinHeight && normH < b.MaxHeight)
                return (b, i);
        }

        return ((BiomeData)Biomes[0], 0);
    }

    // =========================================================
    // ГЕНЕРАЦИЯ ЧАНКА
    // =========================================================
    private void GenerateChunk(Vector2I coord)
    {
        int   res     = ChunkSize + 1;
        float seaH    = Config.SeaLevel * HeightMultiplier;
        float invMax  = 1f / Math.Max(Biomes.Count - 1, 1);

        var heights  = new float[res * res];
        var biomeIdx = new int[res * res];

        // Один проход: сэмплируем шум ровно один раз на вершину
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float wx  = coord.X * ChunkSize + x;
            float wz  = coord.Y * ChunkSize + z;
            float wh  = SampleTerrain(wx, wz, out float normH, out float t, out float hum);
            int   idx = z * res + x;
            heights[idx]  = wh;
            var (_, bi)   = ResolveBiome(normH, t, hum);
            biomeIdx[idx] = bi;
        }

        // Сглаженные нормали через центральные разности
        var normals = ComputeNormals(heights, res);

        // ── Узел чанка ──────────────────────────────────────
        var chunk = new Node3D();
        chunk.Position = new Vector3(coord.X * ChunkSize, 0, coord.Y * ChunkSize);

        // Ландшафтный меш
        var mi = new MeshInstance3D();
        mi.Mesh             = BuildTerrainMesh(heights, normals, biomeIdx, res, invMax);
        mi.MaterialOverride = _terrainMat;
        chunk.AddChild(mi);

        // Плоскость воды (depth-тест скрывает её под сушей)
        var water = new MeshInstance3D();
        water.Mesh             = BuildWaterMesh(seaH);
        water.MaterialOverride = _waterMat;
        water.CastShadow       = GeometryInstance3D.ShadowCastingSetting.Off;
        chunk.AddChild(water);

        // Коллизия: HeightMapShape3D намного быстрее trimesh для ландшафта
        var body = new StaticBody3D();
        var cs   = new CollisionShape3D();
        var hm   = new HeightMapShape3D();
        hm.MapWidth = res;
        hm.MapDepth = res;
        var mapData = new float[heights.Length];
        Array.Copy(heights, mapData, heights.Length);
        hm.MapData  = mapData;
        // Shape центрируется в (0,0) → смещаем на полчанка, чтобы совпасть с мешем
        cs.Position = new Vector3(ChunkSize * 0.5f, 0f, ChunkSize * 0.5f);
        cs.Shape    = hm;
        body.AddChild(cs);
        chunk.AddChild(body);

        AddChild(chunk);
        _chunks[coord] = chunk;

        if (Biomes.Count > 0)
            SpawnObjects(chunk, coord, heights, biomeIdx, res, seaH);
    }

    // =========================================================
    // НОРМАЛИ через центральные разности (O(n), сглаженные)
    // =========================================================
    private static Vector3[] ComputeNormals(float[] h, int res)
    {
        var normals = new Vector3[res * res];
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float L = x > 0     ? h[z * res + x - 1] : h[z * res + x];
            float R = x < res-1 ? h[z * res + x + 1] : h[z * res + x];
            float D = z > 0     ? h[(z-1) * res + x] : h[z * res + x];
            float U = z < res-1 ? h[(z+1) * res + x] : h[z * res + x];
            normals[z * res + x] = new Vector3(L - R, 2f, D - U).Normalized();
        }
        return normals;
    }

    // =========================================================
    // ПОСТРОЕНИЕ ЛАНДШАФТНОГО МЕША (прямые массивы, без SurfaceTool)
    // =========================================================
    private static ArrayMesh BuildTerrainMesh(
        float[] heights, Vector3[] vn, int[] biomeIdx, int res, float invMax)
    {
        int quad = res - 1;
        int vc   = quad * quad * 6;

        var verts   = new Vector3[vc];
        var normals = new Vector3[vc];
        var uvs     = new Vector2[vc];
        var colors  = new Color[vc];

        int vi = 0;
        for (int z = 0; z < quad; z++)
        for (int x = 0; x < quad; x++)
        {
            int i   = z * res + x;
            var col = new Color(biomeIdx[i] * invMax, 0f, 0f, 1f);

            var v00 = new Vector3(x,     heights[i],           z);
            var v10 = new Vector3(x + 1, heights[i + 1],       z);
            var v01 = new Vector3(x,     heights[i + res],     z + 1);
            var v11 = new Vector3(x + 1, heights[i + res + 1], z + 1);

            float u0 = (float)x       / quad;
            float u1 = (float)(x + 1) / quad;
            float v0 = (float)z       / quad;
            float v1 = (float)(z + 1) / quad;

            // Треугольник 1
            verts[vi] = v00; normals[vi] = vn[i];       uvs[vi] = new Vector2(u0, v0); colors[vi] = col; vi++;
            verts[vi] = v10; normals[vi] = vn[i+1];     uvs[vi] = new Vector2(u1, v0); colors[vi] = col; vi++;
            verts[vi] = v01; normals[vi] = vn[i+res];   uvs[vi] = new Vector2(u0, v1); colors[vi] = col; vi++;

            // Треугольник 2
            verts[vi] = v01; normals[vi] = vn[i+res];   uvs[vi] = new Vector2(u0, v1); colors[vi] = col; vi++;
            verts[vi] = v10; normals[vi] = vn[i+1];     uvs[vi] = new Vector2(u1, v0); colors[vi] = col; vi++;
            verts[vi] = v11; normals[vi] = vn[i+res+1]; uvs[vi] = new Vector2(u1, v1); colors[vi] = col; vi++;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV]  = uvs;
        arrays[(int)Mesh.ArrayType.Color]  = colors;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // =========================================================
    // ПЛОСКОСТЬ ВОДЫ (разделена на сетку для анимации волн)
    // =========================================================
    private ArrayMesh BuildWaterMesh(float seaH)
    {
        const int divs = 8;
        int       wRes = divs + 1;
        float     step = (float)ChunkSize / divs;

        var verts   = new Vector3[wRes * wRes];
        var uvs     = new Vector2[wRes * wRes];
        var indices = new int[divs * divs * 6];

        for (int z = 0; z < wRes; z++)
        for (int x = 0; x < wRes; x++)
        {
            verts[z * wRes + x] = new Vector3(x * step, seaH, z * step);
            uvs[z * wRes + x]   = new Vector2((float)x / divs, (float)z / divs);
        }

        int ii = 0;
        for (int z = 0; z < divs; z++)
        for (int x = 0; x < divs; x++)
        {
            int i = z * wRes + x;
            indices[ii++] = i;       indices[ii++] = i + 1;       indices[ii++] = i + wRes;
            indices[ii++] = i + wRes; indices[ii++] = i + 1;      indices[ii++] = i + wRes + 1;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.TexUV]  = uvs;
        arrays[(int)Mesh.ArrayType.Index]  = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // =========================================================
    // СПАВН ОБЪЕКТОВ
    // =========================================================
    private void SpawnObjects(Node3D chunk, Vector2I coord,
                              float[] heights, int[] biomeIdx, int res, float seaH)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)GD.Hash(coord);

        for (int i = 0; i < 25; i++)
        {
            float lx = rng.RandfRange(1f, ChunkSize - 1f);
            float lz = rng.RandfRange(1f, ChunkSize - 1f);
            int   ix = Mathf.Clamp((int)lx, 0, res - 1);
            int   iz = Mathf.Clamp((int)lz, 0, res - 1);
            int   vi = iz * res + ix;

            if (heights[vi] <= seaH) continue;  // под водой объекты не спавним

            var biome = (BiomeData)Biomes[Mathf.Clamp(biomeIdx[vi], 0, Biomes.Count - 1)];
            if (biome.Objects.Count == 0 || rng.Randf() > biome.ObjectChance) continue;

            var scene = biome.Objects[(int)(rng.Randi() % (uint)biome.Objects.Count)];
            var obj   = scene.Instantiate<Node3D>();
            obj.Position = new Vector3(lx, heights[vi], lz);
            obj.Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f);
            chunk.AddChild(obj);
        }
    }

    // =========================================================
    // МАТЕРИАЛ ЛАНДШАФТА
    // =========================================================
    private ShaderMaterial BuildTerrainMaterial()
    {
        // Собираем текстуры биомов
        var images = new Godot.Collections.Array<Image>();
        int tw = -1, th = -1;
        var tf = Image.Format.Rgba8;

        foreach (var res in Biomes)
        {
            var biome = (BiomeData)res;
            if (biome.Texture == null) continue;

            var img = biome.Texture.GetImage();
            img.Decompress();
            if (tw == -1) { tw = img.GetWidth(); th = img.GetHeight(); }
            if (img.GetWidth() != tw || img.GetHeight() != th)
                img.Resize(tw, th, Image.Interpolation.Nearest);
            if (img.GetFormat() != tf)
                img.Convert(tf);
            images.Add(img);
        }

        var shader = new Shader();
        var mat    = new ShaderMaterial();

        if (images.Count > 0)
        {
            // ── Triplanar-проекция через массив текстур биомов ──
            var texArray = new Texture2DArray();
            texArray.CreateFromImages(images);
            int maxId = Math.Max(Biomes.Count - 1, 1);

            shader.Code = BuildTriplanarShader(maxId);
            mat.Shader  = shader;
            mat.SetShaderParameter("biome_textures", texArray);
            mat.SetShaderParameter("triplanar_scale", TriplanarScale);
        }
        else
        {
            // ── Процедурная окраска по высоте + уклону (без текстур) ──
            shader.Code = BuildProceduralShader();
            mat.Shader  = shader;
            mat.SetShaderParameter("sea_height", Config.SeaLevel * HeightMultiplier);
            mat.SetShaderParameter("max_height",  HeightMultiplier);
        }

        return mat;
    }

    // ── Шейдер с triplanar текстурами биомов ──────────────────
    private static string BuildTriplanarShader(int maxId) => $@"
shader_type spatial;
render_mode depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx;

uniform sampler2DArray biome_textures : filter_linear_mipmap_anisotropic, source_color;
uniform float triplanar_scale = 0.08;
uniform float blend_sharpness = 5.0;

varying float biome_id_v;
varying vec3  world_pos_v;
varying vec3  world_nrm_v;   // world-space normal — не зависит от угла камеры

void vertex() {{
    biome_id_v  = COLOR.r * float({maxId});
    world_pos_v = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    // NORMAL в vertex() находится в model-space → переводим в world-space
    world_nrm_v = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}}

vec3 triplanar_sample(sampler2DArray tex, vec3 wpos, vec3 nrm, float id) {{
    vec3 w = pow(abs(nrm), vec3(blend_sharpness));
    w /= max(dot(w, vec3(1.0)), 0.001);   // защита от деления на 0
    return texture(tex, vec3(wpos.zy * triplanar_scale, id)).rgb * w.x
         + texture(tex, vec3(wpos.xz * triplanar_scale, id)).rgb * w.y
         + texture(tex, vec3(wpos.xy * triplanar_scale, id)).rgb * w.z;
}}

void fragment() {{
    // Плавное смешение на границах биомов
    float id_lo = floor(biome_id_v);
    float id_hi = min(id_lo + 1.0, float({maxId}));
    float blend = smoothstep(0.3, 0.7, fract(biome_id_v));

    // Используем world_nrm_v — не зависит от направления взгляда
    vec3 col = mix(
        triplanar_sample(biome_textures, world_pos_v, world_nrm_v, id_lo),
        triplanar_sample(biome_textures, world_pos_v, world_nrm_v, id_hi),
        blend
    );

    // Крутые склоны → порода
    float slope = 1.0 - clamp(dot(world_nrm_v, vec3(0.0, 1.0, 0.0)), 0.0, 1.0);
    vec3  rock  = vec3(0.48, 0.43, 0.38);
    col = mix(col, rock, smoothstep(0.45, 0.82, slope));

    ALBEDO    = col;
    ROUGHNESS = mix(0.9, 0.6, smoothstep(0.45, 0.82, slope));
    SPECULAR  = 0.05;
}}
";

    // ── Процедурный шейдер (когда нет текстур) ─────────────────
    private static string BuildProceduralShader() => @"
shader_type spatial;
render_mode depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx;

uniform float sea_height = 18.0;
uniform float max_height = 48.0;

varying vec3 world_nrm_p;  // world-space normal

void vertex() {
    world_nrm_p = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;

    float h     = clamp(world_pos.y / max_height, 0.0, 1.0);
    float sea_n = sea_height / max_height;
    float slope = 1.0 - clamp(dot(world_nrm_p, vec3(0.0, 1.0, 0.0)), 0.0, 1.0);

    // Цвета биомов
    vec3 sand   = vec3(0.88, 0.81, 0.56);
    vec3 grass  = vec3(0.32, 0.67, 0.24);
    vec3 forest = vec3(0.14, 0.43, 0.17);
    vec3 rock   = vec3(0.50, 0.46, 0.41);
    vec3 snow   = vec3(0.93, 0.95, 0.98);

    float beach  = sea_n + 0.03;
    float plains = sea_n + 0.13;
    float hills  = sea_n + 0.33;
    float high   = sea_n + 0.60;

    vec3 col;
    if      (h < beach)  col = mix(sand,   grass,  smoothstep(sea_n,  beach,  h));
    else if (h < plains) col = mix(grass,  forest, smoothstep(beach,  plains, h));
    else if (h < hills)  col = mix(forest, rock,   smoothstep(plains, hills,  h));
    else if (h < high)   col = mix(rock,   snow,   smoothstep(hills,  high,   h));
    else                 col = snow;

    // Крутые склоны → серая порода (эффект скал)
    col = mix(col, rock, smoothstep(0.45, 0.82, slope));

    ALBEDO    = col;
    ROUGHNESS = mix(0.92, 0.62, smoothstep(0.45, 0.82, slope));
    SPECULAR  = 0.05;
}
";


    // =========================================================
    // МАТЕРИАЛ ВОДЫ
    // =========================================================
    private ShaderMaterial BuildWaterMaterial()
    {
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode cull_back, depth_draw_opaque, diffuse_lambert, specular_schlick_ggx;

uniform vec3  water_shallow : source_color = vec3(0.18, 0.53, 0.84);
uniform vec3  water_deep    : source_color = vec3(0.04, 0.20, 0.50);
uniform float wave_strength = 0.06;
uniform float wave_speed    = 0.55;

void vertex() {
    // Мировые координаты для непрерывных волн между чанками
    vec3 wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    float wave = sin(wpos.x * 0.22 + TIME * wave_speed)        * wave_strength
               + sin(wpos.z * 0.28 + TIME * wave_speed * 1.35) * wave_strength * 0.65
               + sin((wpos.x + wpos.z) * 0.15 + TIME * wave_speed * 0.8) * wave_strength * 0.4;

    VERTEX.y += wave;

    // Анимированные нормали соответствуют форме волн
    float dx = cos(wpos.x * 0.22 + TIME * wave_speed)        * wave_strength * 0.22;
    float dz = cos(wpos.z * 0.28 + TIME * wave_speed * 1.35) * wave_strength * 0.18;
    NORMAL = normalize(vec3(-dx, 1.0, -dz));
}

void fragment() {
    // Fresnel: горизонт = тёмная глубь, взгляд сверху = светлая мель
    float fresnel = pow(1.0 - abs(dot(NORMAL, VIEW)), 2.5);
    ALBEDO    = mix(water_deep, water_shallow, fresnel * 0.6 + 0.28);
    ROUGHNESS = 0.04;
    METALLIC  = 0.0;
    SPECULAR  = 0.95;
}
";
        var mat = new ShaderMaterial();
        mat.Shader = shader;
        mat.SetShaderParameter("water_shallow", new Color(Config.WaterShallowColor.R,
                                                          Config.WaterShallowColor.G,
                                                          Config.WaterShallowColor.B));
        mat.SetShaderParameter("water_deep",    new Color(Config.WaterDeepColor.R,
                                                          Config.WaterDeepColor.G,
                                                          Config.WaterDeepColor.B));
        mat.SetShaderParameter("wave_strength", Config.WaveStrength);
        mat.SetShaderParameter("wave_speed",    Config.WaveSpeed);
        return mat;
    }
}
