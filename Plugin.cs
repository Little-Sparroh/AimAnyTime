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
[MycoMod(null, ModFlags.IsClientSide)]
public class SparrohPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.reloadanytime";
    public const string PluginName = "ReloadAnyTime";
    public const string PluginVersion = "1.0.0";

    internal static new ManualLogSource Logger;
    internal static ConfigEntry<bool> enableCanAimWhileSliding;
    internal static ConfigEntry<bool> enableCanAimWhileReloading;
    internal static ConfigEntry<bool> enableCanAimWhileSprinting;

    private static readonly FieldInfo lockSprintingField = AccessTools.Field(typeof(Gun), "lockSprinting");
    private static readonly FieldInfo gunDataField = AccessTools.Field(typeof(Gun), "gunData");
    private static readonly Dictionary<int, OriginalAimConstraints> originalConstraints = new Dictionary<int, OriginalAimConstraints>();

    private Harmony harmony;
    private FileSystemWatcher configWatcher;
    private static volatile bool pendingConstraintRefresh;

    private struct OriginalAimConstraints
    {
        public FireConstraints.ActionFireMode CanAimWhileSliding;
        public bool CanAimWhileReloading;
        public bool LockSprinting;
    }

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
            "Allows aiming weapons while reloading.");

        enableCanAimWhileSprinting = Config.Bind(
            "General",
            "Can Aim While Sprinting",
            true,
            "Allows aiming weapons while sprinting.");

        enableCanAimWhileSliding.SettingChanged += OnAimConstraintSettingChanged;
        enableCanAimWhileReloading.SettingChanged += OnAimConstraintSettingChanged;
        enableCanAimWhileSprinting.SettingChanged += OnAimConstraintSettingChanged;

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
            MethodInfo setupMethod = AccessTools.Method(typeof(Gun), "Setup", new Type[] { typeof(Player), typeof(PlayerAnimation), typeof(IGear) });
            if (setupMethod == null)
            {
                Logger.LogError("Could not find Gun.Setup method for patching.");
            }
            else
            {
                HarmonyMethod setupPrefix = new HarmonyMethod(typeof(SparrohPlugin), nameof(ModifyWeaponPrefix));
                harmony.Patch(setupMethod, prefix: setupPrefix);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error patching Gun.Setup: {ex.Message}");
        }

        try
        {
            MethodInfo onStartAimMethod = AccessTools.Method(typeof(Gun), "OnStartAim");
            if (onStartAimMethod == null)
            {
                Logger.LogError("Could not find Gun.OnStartAim method!");
            }
            else
            {
                HarmonyMethod onStartAimPrefix = new HarmonyMethod(typeof(SparrohPlugin), nameof(OnStartAimPrefix));
                HarmonyMethod onStartAimPostfix = new HarmonyMethod(typeof(SparrohPlugin), nameof(OnStartAimPostfix));
                harmony.Patch(onStartAimMethod, prefix: onStartAimPrefix, postfix: onStartAimPostfix);
            }

            MethodInfo canAimMethod = AccessTools.Method(typeof(Gun), "CanAim");
            if (canAimMethod == null)
            {
                Logger.LogError("Could not find Gun.CanAim method!");
            }
            else
            {
                HarmonyMethod canAimPrefix = new HarmonyMethod(typeof(SparrohPlugin), nameof(CanAimPrefix));
                harmony.Patch(canAimMethod, prefix: canAimPrefix);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error setting up aiming patches: {ex.Message}");
        }

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully.");
    }

    private void Update()
    {
        if (!pendingConstraintRefresh)
            return;

        pendingConstraintRefresh = false;
        ApplyAimConstraintsToAllGuns();
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

    private static void OnAimConstraintSettingChanged(object sender, EventArgs e)
    {
        pendingConstraintRefresh = true;
    }

    public static void ModifyWeaponPrefix(Gun __instance, IGear prefab)
    {
        if (prefab is not Gun gunPrefab)
            return;

        var aimData = __instance.gameObject.GetComponent<AimStateData>();
        if (aimData == null)
            aimData = __instance.gameObject.AddComponent<AimStateData>();

        ApplyAimConstraints(gunPrefab, aimData);
    }

    internal static void ApplyAimConstraintsToAllGuns()
    {
        try
        {
            Gun[] guns = UnityEngine.Object.FindObjectsOfType<Gun>();
            for (int i = 0; i < guns.Length; i++)
            {
                Gun gun = guns[i];
                if (gun == null)
                    continue;

                var aimData = gun.gameObject.GetComponent<AimStateData>();
                if (aimData == null)
                    aimData = gun.gameObject.AddComponent<AimStateData>();

                ApplyAimConstraints(gun, aimData);
            }

            Logger.LogInfo($"Re-applied aim constraints to {guns.Length} gun(s).");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error re-applying aim constraints: {ex.Message}");
        }
    }

    internal static void ApplyAimConstraints(Gun gun, AimStateData aimData)
    {
        if (gun == null)
            return;

        try
        {
            object gunDataObj = gunDataField?.GetValue(gun);
            if (gunDataObj != null)
            {
                ApplyAimConstraintsToGunDataObject(gun, gunDataObj, aimData);
                return;
            }

            ref var gunData = ref gun.GunData;
            ApplyAimConstraintsToGunData(gun, ref gunData, aimData);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error applying aim constraints: {ex.Message}");
        }
    }

    private static OriginalAimConstraints GetOrCaptureOriginals(
        Gun gun,
        FireConstraints.ActionFireMode canAimWhileSliding,
        bool canAimWhileReloading,
        bool lockSprinting)
    {
        int key = gun.GetInstanceID();
        if (!originalConstraints.TryGetValue(key, out OriginalAimConstraints original))
        {
            original = new OriginalAimConstraints
            {
                CanAimWhileSliding = canAimWhileSliding,
                CanAimWhileReloading = canAimWhileReloading,
                LockSprinting = lockSprinting
            };
            originalConstraints[key] = original;
        }

        return original;
    }

    private static void ApplyAimConstraintsToGunData(Gun gun, ref GunData gunData, AimStateData aimData)
    {
        bool currentLockSprinting = lockSprintingField != null && (bool)lockSprintingField.GetValue(gun);
        OriginalAimConstraints original = GetOrCaptureOriginals(
            gun,
            gunData.fireConstraints.canAimWhileSliding,
            gunData.fireConstraints.canAimWhileReloading,
            currentLockSprinting);

        gunData.fireConstraints.canAimWhileSliding = enableCanAimWhileSliding.Value
            ? FireConstraints.ActionFireMode.CanPerformDuring
            : original.CanAimWhileSliding;

        gunData.fireConstraints.canAimWhileReloading = enableCanAimWhileReloading.Value
            ? true
            : original.CanAimWhileReloading;

        if (aimData != null)
            aimData.CanAimWhileSprinting = enableCanAimWhileSprinting.Value;

        if (lockSprintingField != null)
        {
            bool lockSprintingValue = enableCanAimWhileSprinting.Value
                ? false
                : original.LockSprinting;
            lockSprintingField.SetValue(gun, lockSprintingValue);
        }
    }

    private static void ApplyAimConstraintsToGunDataObject(Gun gun, object gunDataObj, AimStateData aimData)
    {
        FieldInfo fireConstraintsField = gunDataObj.GetType().GetField("fireConstraints");
        if (fireConstraintsField == null)
            return;

        object fireConstraints = fireConstraintsField.GetValue(gunDataObj);
        if (fireConstraints == null)
            return;

        Type constraintsType = fireConstraints.GetType();
        FieldInfo slideField = constraintsType.GetField("canAimWhileSliding");
        FieldInfo reloadField = constraintsType.GetField("canAimWhileReloading");
        if (slideField == null || reloadField == null)
            return;

        bool currentLockSprinting = lockSprintingField != null && (bool)lockSprintingField.GetValue(gun);
        OriginalAimConstraints original = GetOrCaptureOriginals(
            gun,
            (FireConstraints.ActionFireMode)slideField.GetValue(fireConstraints),
            (bool)reloadField.GetValue(fireConstraints),
            currentLockSprinting);

        object slideValue = enableCanAimWhileSliding.Value
            ? FireConstraints.ActionFireMode.CanPerformDuring
            : original.CanAimWhileSliding;
        object reloadValue = enableCanAimWhileReloading.Value
            ? true
            : original.CanAimWhileReloading;

        slideField.SetValue(fireConstraints, slideValue);
        reloadField.SetValue(fireConstraints, reloadValue);
        fireConstraintsField.SetValue(gunDataObj, fireConstraints);

        if (gunDataField != null && gunDataField.FieldType.IsValueType)
            gunDataField.SetValue(gun, gunDataObj);

        if (aimData != null)
            aimData.CanAimWhileSprinting = enableCanAimWhileSprinting.Value;

        if (lockSprintingField != null)
        {
            bool lockSprintingValue = enableCanAimWhileSprinting.Value
                ? false
                : original.LockSprinting;
            lockSprintingField.SetValue(gun, lockSprintingValue);
        }
    }

    public static bool OnStartAimPrefix(Gun __instance)
    {
        try
        {
            var aimData = __instance.gameObject.GetComponent<AimStateData>();
            if (aimData != null && aimData.CanAimWhileSprinting)
            {
                FieldInfo playerField = AccessTools.Field(typeof(Gun), "player");
                if (playerField != null)
                {
                    Player player = (Player)playerField.GetValue(__instance);
                    if (player != null)
                    {
                        PropertyInfo isSprintingProp = AccessTools.Property(typeof(Player), "IsSprinting");
                        if (isSprintingProp != null)
                        {
                            aimData.WasSprinting = (bool)isSprintingProp.GetValue(player);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in OnStartAimPrefix: {ex.Message}");
        }
        return true;
    }

    public static void OnStartAimPostfix(Gun __instance)
    {
        try
        {
            var aimData = __instance.gameObject.GetComponent<AimStateData>();
            if (aimData != null && aimData.CanAimWhileSprinting && aimData.WasSprinting)
            {
                FieldInfo playerField = AccessTools.Field(typeof(Gun), "player");
                if (playerField != null)
                {
                    Player player = (Player)playerField.GetValue(__instance);
                    if (player != null)
                    {
                        MethodInfo resumeSprintMethod = AccessTools.Method(typeof(Player), "ResumeSprint");
                        if (resumeSprintMethod != null)
                        {
                            resumeSprintMethod.Invoke(player, null);
                        }
                    }
                }
                aimData.WasSprinting = false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in OnStartAimPostfix: {ex.Message}");
        }
    }

    public static bool CanAimPrefix(Gun __instance, ref bool __result)
    {
        try
        {
            var aimData = __instance.gameObject.GetComponent<AimStateData>();
            if (aimData != null && aimData.CanAimWhileSprinting)
            {
                FieldInfo playerField = AccessTools.Field(typeof(Gun), "player");
                if (playerField != null)
                {
                    Player player = (Player)playerField.GetValue(__instance);
                    if (player != null)
                    {
                        PropertyInfo isSprintingProp = AccessTools.Property(typeof(Player), "IsSprinting");
                        if (isSprintingProp != null && (bool)isSprintingProp.GetValue(player))
                        {
                            FieldInfo isAimInputHeldField = AccessTools.Field(typeof(Gun), "isAimInputHeld");
                            if (isAimInputHeldField != null)
                            {
                                bool isAimInputHeld = (bool)isAimInputHeldField.GetValue(__instance);
                                if (isAimInputHeld)
                                {
                                    __result = true;
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in CanAimPrefix: {ex.Message}");
        }
        return true;
    }

    private void OnDestroy()
    {
        if (enableCanAimWhileSliding != null)
            enableCanAimWhileSliding.SettingChanged -= OnAimConstraintSettingChanged;
        if (enableCanAimWhileReloading != null)
            enableCanAimWhileReloading.SettingChanged -= OnAimConstraintSettingChanged;
        if (enableCanAimWhileSprinting != null)
            enableCanAimWhileSprinting.SettingChanged -= OnAimConstraintSettingChanged;

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

public class AimStateData : MonoBehaviour
{
    public bool CanAimWhileSprinting { get; set; }
    public bool WasSprinting { get; set; }
}
