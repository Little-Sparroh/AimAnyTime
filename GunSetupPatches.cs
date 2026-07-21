using System;
using HarmonyLib;
using Pigeon.Movement;

/// <summary>
/// Applies equip-time tweaks (lockSprinting, sliding override) when guns are set up / enabled.
/// lockSprinting must be set in a prefix on Enable because Enable increments SprintLocks from that field.
/// </summary>
[HarmonyPatch]
public static class GunSetupPatches
{
    [HarmonyPatch(typeof(Gun), "Setup", new Type[] { typeof(Player), typeof(PlayerAnimation), typeof(IGear) })]
    [HarmonyPostfix]
    private static void SetupPostfix(Gun __instance)
    {
        try
        {
            SparrohPlugin.ApplyLockSprinting(__instance);
            SparrohPlugin.ApplySlidingOverrideToLocalPlayer();
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in Gun.Setup patch: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(Gun), "Enable")]
    [HarmonyPrefix]
    private static void EnablePrefix(Gun __instance)
    {
        try
        {
            // Must run before Enable reads lockSprinting for SprintLocks++.
            SparrohPlugin.ApplyLockSprinting(__instance);
            SparrohPlugin.ApplySlidingOverrideToLocalPlayer();
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in Gun.Enable prefix: {ex.Message}");
        }
    }
}
