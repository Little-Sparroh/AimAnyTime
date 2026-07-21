# Aim Any Time

A BepInEx mod for Mycopunk that lets you aim while reloading, sliding, and sprinting — without losing reload animation feedback.

## Features

- **Aim While Reloading**: Stay in ADS (FOV/aim state) during reloads.
- **Show Reload Anim While Aiming**: Temporarily hide the aim pose during reload so the reload animation is visible and you can tell when you're ready to fire again.
- **Aim While Sliding**: Allow aiming during slides (uses the same player override as the vanilla Aim While Sliding upgrade).
- **Aim While Sprinting**: Allow aiming during sprint without cancelling sprint.

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
1. Place the built `AimAnyTime.dll` in your `<Mycopunk Directory>/BepInEx/plugins/` folder

### Executing program

The mod loads automatically through BepInEx when the game starts. Check the BepInEx console for loading confirmation messages.

## Configuration

Access mod settings through the BepInEx configuration file at `<Mycopunk Directory>/BepInEx/config/sparroh.aimanytime.cfg`:

| Setting | Default | Description |
|---------|---------|-------------|
| Can Aim While Sliding | `true` | Allows aiming weapons while sliding. |
| Can Aim While Reloading | `true` | Keeps ADS active while reloading (FOV/aim state). |
| Can Aim While Sprinting | `true` | Allows aiming weapons while sprinting without cancelling sprint. |
| Show Reload Anim While Aiming | `true` | While ADS-reloading, hide the aim pose so the reload animation is visible. |

Config changes are hot-reloaded while the game is running.

## How it works (short)

Vanilla ADS uses a full (non-additive) aim animation layer that covers the base layer. Enabling aim-while-reload alone keeps that layer up and hides the reload. This mod keeps ADS state on during reload, but drops the aim layer for the reload so the clip and its ammo events stay readable.

## Help

* **Mod not loading?** Verify BepInEx is installed correctly and check console logs for errors
* **Reload still invisible?** Ensure both `Can Aim While Reloading` and `Show Reload Anim While Aiming` are enabled
* **Aiming still blocked while sprinting?** Ensure `Can Aim While Sprinting` is enabled; some weapons use a sprint lock that this setting clears

## Authors

- Sparroh

## License

This project is licensed under the MIT License - see the LICENSE file for details
