using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Pigeon.Movement;
using UnityEngine;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[MycoMod(null, ModFlags.IsSandbox)]
public class SparrohPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.aimanytime";
    public const string PluginName = "AimAnyTime";
    public const string PluginVersion = "1.1.0";


    internal static new ManualLogSource Logger;

    internal static ConfigEntry<bool> enableCanAimWhileSliding;
    internal static ConfigEntry<bool> enableCanAimWhileReloading;
    internal static ConfigEntry<bool> enableCanAimWhileSprinting;
    internal static ConfigEntry<bool> showReloadAnimWhileAiming;

    private static readonly FieldInfo lockSprintingField = AccessTools.Field(typeof(Gun), "lockSprinting");
    private static readonly Dictionary<int, bool> originalLockSprinting = new Dictionary<int, bool>();

    private Harmony harmony;
    private FileSystemWatcher configWatcher;
    private static volatile bool pendingConstraintRefresh;

    private void Awake()
    {
        Logger = base.Logger;

        enableCanAimWhileSliding = Config.Bind(
            "General",
            "Can Aim While Sliding",
            true,
            "Allows aiming weapons while sliding.");

        enableCanAimWhileReloading = Config.Bind(
            "General",
            "Can Aim While Reloading",
            true,
            "Keeps ADS active while reloading (FOV/aim state). Pair with Show Reload Anim While Aiming for visible reload feedback.");

        enableCanAimWhileSprinting = Config.Bind(
            "General",
            "Can Aim While Sprinting",
            true,
            "Allows aiming weapons while sprinting without cancelling sprint.");

        showReloadAnimWhileAiming = Config.Bind(
            "General",
            "Show Reload Anim While Aiming",
            true,
            "While ADS-reloading, temporarily hide the aim pose so the reload animation is visible. ADS state (FOV) stays active.");

        enableCanAimWhileSliding.SettingChanged += OnSettingChanged;
        enableCanAimWhileReloading.SettingChanged += OnSettingChanged;
        enableCanAimWhileSprinting.SettingChanged += OnSettingChanged;
        showReloadAnimWhileAiming.SettingChanged += OnSettingChanged;

        try
        {
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error setting up config file watcher: {ex.Message}");
        }

        harmony = new Harmony(PluginGUID);

        try
        {
            harmony.PatchAll(typeof(CanAimPatches));
            harmony.PatchAll(typeof(ReloadAimAnimPatches));
            harmony.PatchAll(typeof(GunSetupPatches));
            Logger.LogInfo("Harmony patches applied.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error applying patches: {ex.Message}");
        }

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully.");
    }

    private void Update()
    {
        if (!pendingConstraintRefresh)
            return;

        pendingConstraintRefresh = false;
        ApplyLockSprintingToAllGuns();
        ApplySlidingOverrideToLocalPlayer();
    }

    private void SetupFileWatcher()
    {
        configWatcher = new FileSystemWatcher(Paths.ConfigPath, $"{PluginGUID}.cfg");
        configWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        configWatcher.Changed += OnConfigFileChanged;
        configWatcher.Created += OnConfigFileChanged;
        configWatcher.Renamed += OnConfigFileChanged;
        configWatcher.EnableRaisingEvents = true;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            Config.Reload();
            pendingConstraintRefresh = true;
            Logger.LogInfo("Config reloaded from disk.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reloading config: {ex.Message}");
        }
    }

    private static void OnSettingChanged(object sender, EventArgs e)
    {
        pendingConstraintRefresh = true;
    }

    internal static void ApplySlidingOverrideToLocalPlayer()
    {
        try
        {
            Player player = Player.LocalPlayer;
            if (player == null)
                return;

            // Matches AimWhileSlidingUpgrade: player-level override beats per-gun defaults.
            player.OverrideCanAimWhileSliding = enableCanAimWhileSliding.Value
                ? FireConstraints.ActionFireMode.CanPerformDuring
                : FireConstraints.ActionFireMode.CannotPerformDuring;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error applying sliding aim override: {ex.Message}");
        }
    }

    internal static void ApplyLockSprintingToAllGuns()
    {
        try
        {
            Gun[] guns = UnityEngine.Object.FindObjectsOfType<Gun>();
            for (int i = 0; i < guns.Length; i++)
            {
                if (guns[i] != null)
                    ApplyLockSprinting(guns[i]);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error re-applying lockSprinting: {ex.Message}");
        }
    }

    internal static void ApplyLockSprinting(Gun gun)
    {
        if (gun == null || lockSprintingField == null)
            return;

        try
        {
            int key = gun.GetInstanceID();
            bool current = (bool)lockSprintingField.GetValue(gun);

            if (!originalLockSprinting.TryGetValue(key, out bool original))
            {
                // First time we see this instance: capture vanilla value before we mutate it.
                original = current;
                originalLockSprinting[key] = original;
            }

            // lockSprinting increments SprintLocks on equip and blocks sprint while the gun is held.
            bool desired = enableCanAimWhileSprinting.Value ? false : original;
            if (current == desired)
                return;

            lockSprintingField.SetValue(gun, desired);

            // If the gun is already equipped, keep SprintLocks in sync with the field Enable/Disable use.
            if (gun.Active)
            {
                Player player = gun.Player;
                if (player != null)
                {
                    if (current && !desired)
                        player.SprintLocks = Math.Max(0, player.SprintLocks - 1);
                    else if (!current && desired)
                        player.SprintLocks++;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error applying lockSprinting: {ex.Message}");
        }
    }


    private void OnDestroy()
    {
        if (enableCanAimWhileSliding != null)
            enableCanAimWhileSliding.SettingChanged -= OnSettingChanged;
        if (enableCanAimWhileReloading != null)
            enableCanAimWhileReloading.SettingChanged -= OnSettingChanged;
        if (enableCanAimWhileSprinting != null)
            enableCanAimWhileSprinting.SettingChanged -= OnSettingChanged;
        if (showReloadAnimWhileAiming != null)
            showReloadAnimWhileAiming.SettingChanged -= OnSettingChanged;

        if (configWatcher != null)
        {
            configWatcher.EnableRaisingEvents = false;
            configWatcher.Changed -= OnConfigFileChanged;
            configWatcher.Created -= OnConfigFileChanged;
            configWatcher.Renamed -= OnConfigFileChanged;
            configWatcher.Dispose();
            configWatcher = null;
        }

        harmony?.UnpatchSelf();
    }
}
