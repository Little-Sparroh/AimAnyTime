using System;
using System.Reflection;
using HarmonyLib;
using Pigeon.Movement;
using UnityEngine;

/// <summary>
/// Replaces Gun.CanAim so slide/reload/sprint aim rules are consistent and don't
/// fight vanilla StopSprint / ResumeSprint side effects when those features are enabled.
/// </summary>
[HarmonyPatch]
public static class CanAimPatches
{
    private static readonly FieldInfo isAimInputHeldField = AccessTools.Field(typeof(Gun), "isAimInputHeld");
    private static readonly FieldInfo lastPressedAimTimeField = AccessTools.Field(typeof(Gun), "lastPressedAimTime");
    private static readonly FieldInfo lastPressedFireTimeField = AccessTools.Field(typeof(Gun), "lastPressedFireTime");
    private static readonly FieldInfo gunDataField = AccessTools.Field(typeof(Gun), "gunData");

    [HarmonyPatch(typeof(Gun), "CanAim")]
    [HarmonyPrefix]
    private static bool CanAimPrefix(Gun __instance, ref bool __result)
    {
        try
        {
            Player player = __instance.Player;
            if (player == null || isAimInputHeldField == null || gunDataField == null)
                return true;

            bool isAimInputHeld = (bool)isAimInputHeldField.GetValue(__instance);
            GunData gunData = (GunData)gunDataField.GetValue(__instance);
            FireConstraints constraints = gunData.fireConstraints;

            // Sliding: player override (mod/upgrade) or per-gun constraint, same as vanilla.
            FireConstraints.ActionFireMode slideMode =
                player.OverrideCanAimWhileSliding > constraints.canAimWhileSliding
                    ? player.OverrideCanAimWhileSliding
                    : constraints.canAimWhileSliding;

            if (SparrohPlugin.enableCanAimWhileSliding.Value)
                slideMode = FireConstraints.ActionFireMode.CanPerformDuring;

            bool canAimWhileSliding =
                slideMode > FireConstraints.ActionFireMode.CannotPerformDuring || !player.Sliding;

            bool canAimWhileReloading =
                !__instance.Reloading
                || constraints.canAimWhileReloading
                || SparrohPlugin.enableCanAimWhileReloading.Value;

            bool flag = isAimInputHeld
                        && canAimWhileSliding
                        && canAimWhileReloading
                        && !player.IsFireLocked;

            if (flag)
            {
                if (player.IsSprinting)
                {
                    if (SparrohPlugin.enableCanAimWhileSprinting.Value)
                    {
                        // Soft ADS while sprinting: keep sprint, keep aim.
                    }
                    else
                    {
                        // Vanilla: sprint started after aim press cancels aim; otherwise stop sprint to ADS.
                        float lastPressedAimTime = lastPressedAimTimeField != null
                            ? (float)lastPressedAimTimeField.GetValue(__instance)
                            : 0f;

                        if (player.StartSprintTime >= lastPressedAimTime)
                            flag = false;
                        else
                            player.StopSprint();
                    }
                }
            }
            else if (__instance.IsAiming
                     && !__instance.WantsToFire
                     && !SparrohPlugin.enableCanAimWhileSprinting.Value)
            {
                // Vanilla resume-sprint path only when we still cancel sprint to aim.
                float lastPressedFireTime = lastPressedFireTimeField != null
                    ? (float)lastPressedFireTimeField.GetValue(__instance)
                    : 0f;

                if (Time.time - Mathf.Max(__instance.LastFireTime, lastPressedFireTime) > 0.5f)
                    player.ResumeSprint();
            }

            __result = flag;
            return false;
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in CanAim patch: {ex.Message}");
            return true;
        }
    }
}
