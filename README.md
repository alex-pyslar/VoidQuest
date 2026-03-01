# VOID QUEST

A 3D action-RPG built with **Godot 4.6 + C#** featuring procedurally generated worlds,
real-time combat, crafting, inventory management, and LAN multiplayer.

[▶ Play VoidQuest on itch.io](https://alexpyslar.itch.io/voidquest)

---

## Features

### World
- **Procedural terrain** — 6-layer noise (continental, erosion, peaks, temperature, humidity, rivers) inspired by Minecraft's terrain system
- **Multiple biomes** — Ocean, Desert, Plains, Forest, Snow, Mountains — each with unique textures, colors, and objects
- **Dynamic chunk streaming** — terrain loads and unloads as you explore; configurable view distance
- **Reproducible worlds** — set the `Seed` export on WorldGenerator to share and replay exact maps

### Combat & Enemies
- **Three enemy variants**: Normal, Fast (speed ×1.8), Tank (HP ×3)
- **Spells**: Fireball (Q), AOE Blast (E), Teleport Blink (R)
- **Melee attack** — left mouse button or F
- **Level-up system** — XP from kills, stat bonuses per level

### Inventory & Crafting
- **8×4 grid inventory** — 32 item slots with category-colour coding
- **Equipment slots** — Weapon, Armor, Shield, Ring
- **65+ items** — weapons, armours, consumables, materials
- **50+ crafting recipes** — gather materials, open crafting tab, click Craft

### Treasure Chests
- Dynamic chest pool that spawns around the player (up to 15 chests)
- Walk into a chest to open it — animated lid, scattered loot
- Chests despawn as you move away; new ones appear ahead of you

### Multiplayer (LAN)
- **Host / Join** over a local network using ENet (UDP)
- Up to **8 players** simultaneously
- Each player has full authority over their own character
- **Dedicated server** support — run headless on a machine without a display

### UI
- Retro minimalist aesthetic — dark indigo palette, cyan borders, sharp corners, gold highlights
- **HUD**: Health/Mana bars, minimap, spell hotbar, score, level, coordinates, enemy counter
- **Pause menu** (Esc) and **Game Over** screen
- **Russian / English** language support — toggle in Main Menu → Settings

---

## Controls

| Action         | Key / Button        |
|----------------|---------------------|
| Move           | W A S D             |
| Look           | Mouse               |
| Sprint         | Shift (hold)        |
| Jump           | Space               |
| Attack (melee) | Left Mouse / F      |
| Spell 1        | Q (Fireball)        |
| Spell 2        | E (AOE Blast)       |
| Spell 3        | R (Teleport Blink)  |
| Inventory      | I or Tab            |
| Pause          | Esc                 |

---

## Requirements

| Tool | Version |
|------|---------|
| [Godot Engine](https://godotengine.org/) | 4.6+ with .NET support |
| [.NET SDK](https://dotnet.microsoft.com/) | 8.0+ |
| Windows export templates | for building only |

---

## Getting Started (from source)

```bash
# 1. Clone
git clone <repo-url>
cd void-quest

# 2. Open in Godot 4.6
#    File -> Open Project -> select the void-quest/ folder
#    Press F5 (or the Play button) to run

# 3. Build a standalone executable (requires export templates)
./build.sh          # Linux / macOS
build.bat           # Windows
```

---

## Running

### Play the built executable

```bash
./run.sh            # Linux / macOS
run.bat             # Windows
```

### Run directly from the Godot editor

```bash
./run.sh editor
run.bat editor
```

---

## Multiplayer

### Host a LAN game

1. Launch the game → **Multiplayer**
2. Click **Host Game** — your LAN IP is displayed
3. Share the IP with friends
4. Click **Start** once everyone has joined

### Join a LAN game

1. **Multiplayer** → **Join Game**
2. Enter the host's LAN IP
3. Click **Connect**

---

## Dedicated Server

The dedicated server runs **headless** (no window, no rendering) and manages all
player connections. Players join it exactly like a hosted game.

### Start the server

```bash
# Windows
run_server.bat

# Linux / macOS
chmod +x run_server.sh
./run_server.sh

# Background, survives SSH logout (Linux)
nohup ./run_server.sh > server.log 2>&1 &
```

What the server does:

- Binds **UDP port 7777** automatically
- Loads the World scene with no UI
- Prints connection events to the console
- Shuts down cleanly on **Ctrl+C**

### Firewall / port forwarding

Open **UDP 7777** on the server machine.
For internet play, forward this port on your router and share the WAN IP with players.

### Server configuration

| Setting | File | Default |
|---------|------|---------|
| Port | `Scripts/NetworkManager.cs` → `DefaultPort` | `7777` |
| Max players | `Scripts/NetworkManager.cs` → `MaxPlayers` | `8` |
| World seed | WorldGenerator node → `Seed` export | `0` (random) |
| Max enemies | EnemySpawner node → `MaxEnemies` | `20` |
| Max chests | ChestSpawner node → `MaxChests` | `15` |

---

## Reproducible Worlds

Every run prints the active world seed to the console:

```
[World] Seed: 1847392610  (set the Seed export to reproduce this world)
```

To replay a specific world:

1. Open **World.tscn** in the Godot editor
2. Select the **WorldGenerator** node
3. Set the **Seed** export field to the printed value

---

## Building

### Windows

```bat
:: Optional: point to your Godot 4.6 exe if godot4 is not in PATH
set GODOT4=C:\Tools\Godot_v4.6\Godot.exe

build.bat          :: release
build.bat debug    :: debug
```

### Linux / macOS

```bash
# Optional: point to your Godot 4.6 binary
export GODOT4=/opt/godot4/godot4

./build.sh          # release
./build.sh debug    # debug
```

Output goes to `build/`.

---

## Project Structure

```
void-quest/
├── Assets/
│   └── Textures/              terrain, water, UI textures
├── Scenes/
│   ├── MainMenu.tscn           main menu (play, multiplayer, settings)
│   ├── LobbyMenu.tscn          multiplayer lobby
│   ├── World.tscn              main game scene
│   ├── HUD.tscn                in-game UI
│   ├── Player.tscn             player character
│   ├── Enemy.tscn              enemy character
│   ├── Chest.tscn              treasure chest
│   ├── LootItem.tscn           dropped collectible item
│   └── Objects/                trees, rocks, props
├── Scripts/
│   ├── Player.cs               movement, combat, spells, inventory
│   ├── Enemy.cs                AI, three enemy variants
│   ├── WorldGenerator.cs       procedural terrain & chunk streaming
│   ├── WorldSetup.cs           player spawning (single + multiplayer)
│   ├── WorldConfig.cs          terrain parameters (Resource)
│   ├── BiomeData.cs            biome definition (Resource)
│   ├── TreasureChest.cs        chest open logic, loot scatter
│   ├── ChestSpawner.cs         dynamic chest pool management
│   ├── EnemySpawner.cs         dynamic enemy pool management
│   ├── Inventory.cs            grid inventory, equipment slots
│   ├── ItemDatabase.cs         65+ item definitions
│   ├── CraftingDatabase.cs     50+ crafting recipes
│   ├── LootItem.cs             world-space collectible
│   ├── Hud.cs                  HUD logic
│   ├── GameManager.cs          game state machine         [autoload]
│   ├── ScoreManager.cs         score tracking             [autoload]
│   ├── NetworkManager.cs       ENet host/join, peer list  [autoload]
│   ├── LocaleManager.cs        RU/EN translations         [autoload]
│   ├── DedicatedServer.cs      headless server bootstrap  [autoload]
│   ├── MainMenu.cs             main menu controller
│   └── LobbyMenu.cs            lobby controller
├── build/
│   ├── VoidQuest.exe           Windows game executable
│   └── VoidQuest.console.exe   Windows headless executable (server)
├── build.bat                   Windows build script
├── build.sh                    Linux/macOS build script
├── run.bat                     Windows run script
├── run.sh                      Linux/macOS run script
├── run_server.bat              Windows dedicated server
├── run_server.sh               Linux/macOS dedicated server
├── export_presets.cfg          Godot export configuration
└── project.godot               Godot project settings
```

---

## Technologies

| Technology | Purpose |
|------------|---------|
| Godot 4.6 (Forward+) | Engine, rendering, physics |
| C# / .NET 8 | All game logic and scripts |
| ENet (UDP) | Multiplayer networking |
| FastNoiseLite | Procedural terrain generation |
| Godot Multiplayer API | RPC, authority, peer management |

---

## License

MIT — see [LICENSE](LICENSE) for details.
