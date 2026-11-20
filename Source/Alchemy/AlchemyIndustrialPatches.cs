using Axolotl;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    // Fixed-time industrial targets store integer active ticks in the native
    // _refineProgress field. Their progress max is tick-based, not recipe time.
    [HarmonyPatch(typeof(Building_AlchemyStove_Base), nameof(Building_AlchemyStove_Base.CurrentTargetPill_ProgressMax), MethodType.Getter)]
    internal static class AlchemyFixedProgressMax
    {
        private static void Postfix(Building_AlchemyStove_Base __instance, ref float __result)
        {
            if (!(__instance is Building_AlchemyStove_Industrial)) return;
            AlchemyRecipeExtension config = __instance.TargetPill?.GetModExtension<AlchemyRecipeExtension>();
            if (config != null && config.fixedActiveTicks > 0) __result = config.fixedActiveTicks;
        }
    }

    [HarmonyPatch(typeof(Building_AlchemyStove_Industrial), nameof(Building_AlchemyStove_Industrial.UpdateAlstProgressAndConsumeConnectDestFuel))]
    internal static class AlchemyFixedProgressTick
    {
        private const float FixedFuelPerInterval = 5000f / 60000f;

        private static bool Prefix(Building_AlchemyStove_Industrial __instance, int delta)
        {
            AlchemyRecipeExtension config = __instance.TargetPill?.GetModExtension<AlchemyRecipeExtension>();
            if (config == null || config.fixedActiveTicks <= 0) return true;

            if (!__instance.IsConnected || !__instance.IsStuffEnough || !__instance.IsStart) return false;
            if (!__instance.IsHashIntervalTick(60, delta)) return false;

            CompRefuelable fuel = __instance.Comp_Refuelable;
            if (fuel == null || fuel.Fuel + 0.0001f < FixedFuelPerInterval) return false;

            fuel.ConsumeFuel(FixedFuelPerInterval);
            float max = __instance.CurrentTargetPill_ProgressMax;
            float next = __instance.CurrentTargetPill_Progress + 60f;
            if (next >= max)
            {
                __instance.CurrentTargetPill_Progress = max;
                __instance.Action_FinishRefine(true);
            }
            else
            {
                __instance.CurrentTargetPill_Progress = next;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Building_AlchemyStove_Industrial), nameof(Building_AlchemyStove_Industrial.ProgramEfficiencyPerTick), MethodType.Getter)]
    internal static class AlchemyFixedProgressUi
    {
        private static void Postfix(Building_AlchemyStove_Industrial __instance, ref float __result)
        {
            if (!__instance.IsConnected) return;
            AlchemyRecipeExtension config = __instance.TargetPill?.GetModExtension<AlchemyRecipeExtension>();
            if (config != null && config.fixedActiveTicks > 0) __result = 1f;
        }
    }
}
