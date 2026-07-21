using System;
using System.Reflection;
using HarmonyLib;

/// <summary>
/// Soft ADS during reload: keep IsAiming (FOV/reticle/logic) but drop the aim animation
/// layer so the base-layer reload clip is visible and animation events still drive ammo.
/// </summary>
[HarmonyPatch]
public static class ReloadAimAnimPatches
{
    private static readonly FieldInfo animatorField = AccessTools.Field(typeof(Gun), "animator");
    private static readonly FieldInfo aimTransitionDurationField = AccessTools.Field(typeof(Gun), "aimTransitionDuration");
    private static readonly PropertyInfo enableAimAnimationsProperty = AccessTools.Property(typeof(Gun), "EnableAimAnimations");

    private static bool ShouldShowReloadOverAim(Gun gun)
    {
        return SparrohPlugin.enableCanAimWhileReloading != null
               && SparrohPlugin.enableCanAimWhileReloading.Value
               && SparrohPlugin.showReloadAnimWhileAiming != null
               && SparrohPlugin.showReloadAnimWhileAiming.Value
               && gun != null
               && gun.Reloading
               && gun.IsAiming;
    }

    private static void HideAimLayerForReload(Gun gun, bool instant)
    {
        if (animatorField == null)
            return;

        var animator = animatorField.GetValue(gun) as PlayerAnimation;
        if (animator == null)
            return;

        float duration = 0f;
        if (!instant && aimTransitionDurationField != null)
            duration = (float)aimTransitionDurationField.GetValue(gun);

        // fadeDuration 0 snaps the aim layer off so reload is immediately readable.
        animator.DisableAimLayer(duration);
    }

    /// <summary>
    /// HandleAim enables the aim layer every frame while IsAiming. After it runs,
    /// force the layer back down for the duration of a reload so the reload anim shows.
    /// </summary>
    [HarmonyPatch(typeof(Gun), "HandleAim")]
    [HarmonyPostfix]
    private static void HandleAimPostfix(Gun __instance)
    {
        try
        {
            if (!ShouldShowReloadOverAim(__instance))
                return;

            // Respect guns that disable aim animations entirely.
            if (enableAimAnimationsProperty != null
                && enableAimAnimationsProperty.GetValue(__instance) is bool enabled
                && !enabled)
                return;

            HideAimLayerForReload(__instance, instant: true);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in HandleAim reload-anim patch: {ex.Message}");
        }
    }

    /// <summary>
    /// Drop the aim layer as soon as reload starts so there isn't a one-frame aim flash.
    /// </summary>
    [HarmonyPatch(typeof(Gun), "StartReloadAnimation")]
    [HarmonyPostfix]
    private static void StartReloadAnimationPostfix(Gun __instance)
    {
        try
        {
            if (SparrohPlugin.enableCanAimWhileReloading == null
                || !SparrohPlugin.enableCanAimWhileReloading.Value
                || SparrohPlugin.showReloadAnimWhileAiming == null
                || !SparrohPlugin.showReloadAnimWhileAiming.Value)
                return;

            if (__instance == null || !__instance.IsAiming)
                return;

            HideAimLayerForReload(__instance, instant: true);
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in StartReloadAnimation patch: {ex.Message}");
        }
    }
}
