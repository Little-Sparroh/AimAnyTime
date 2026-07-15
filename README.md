# Reload Any Time

A BepInEx mod for Mycopunk that improves aiming constraints so you can aim while reloading, sliding, and sprinting.

## Features

- **Aim While Reloading**: Keep aiming during reloads.
- **Aim While Sliding**: Allow aiming during slides.
- **Aim While Sprinting**: Allow aiming during sprint and resume sprint after aiming when appropriate.

## Getting Started

### Dependencies

* Mycopunk (base game)
* [BepInEx](https://github.com/BepInEx/BepInEx) - Version 5.4.2403 or compatible
* .NET Framework 4.8
* [HarmonyLib](https://github.com/pardeike/Harmony) (included via NuGet)

### Building/Compiling

1. Clone this repository
2. Open the solution file in Visual Studio, Rider, or your preferred C# IDE
3. Build the project in Release mode to generate the .dll file

Alternatively, use dotnet CLI:
```bash
dotnet build --configuration Release
```

### Installing

**Via Thunderstore (Recommended)**:
1. Download and install via Thunderstore Mod Manager
2. The mod will be automatically installed to the correct directory

**Manual Installation**:
1. Place the built `ReloadAnyTime.dll` in your `<Mycopunk Directory>/BepInEx/plugins/` folder

### Executing program

The mod loads automatically through BepInEx when the game starts. Check the BepInEx console for loading confirmation messages.

## Configuration

Access mod settings through the BepInEx configuration file at `<Mycopunk Directory>/BepInEx/config/sparroh.reloadanytime.cfg`:

| Setting | Default | Description |
|---------|---------|-------------|
| Can Aim While Sliding | `true` | Allows aiming weapons while sliding. |
| Can Aim While Reloading | `true` | Allows aiming weapons while reloading. |
| Can Aim While Sprinting | `true` | Allows aiming weapons while sprinting. |

Config changes are hot-reloaded while the game is running. Editing the `.cfg` file (or changing settings in-game) re-applies aim constraints to equipped weapons without a restart. Disabling a setting restores the weapon's original constraint values.

## Help

* **Mod not loading?** Verify BepInEx is installed correctly and check console logs for errors
* **Aiming still blocked?** Ensure the relevant config options are enabled; changes apply immediately after save


## Authors

- Sparroh

## License

This project is licensed under the MIT License - see the LICENSE file for details
