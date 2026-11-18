using HarmonyLib;
using MoeLotlExpansion.Core;
using RigorMortis;
using Verse;

namespace MoeLotlExpansion
{
    [HarmonyPatch(typeof(CompYinAndMalevolent), nameof(CompYinAndMalevolent.MalevolentCapacity), MethodType.Getter)]
    internal static class CapacityPillPatch
    {
        private static void Postfix(CompYinAndMalevolent __instance, ref float __result)
        {
            if (ZombieMedicineService.Active(__instance.Zombie, MXDefOf.MX_CapacityEffect)) __result *= 1.5f;
        }
    }

    internal static class PurifyPolicy
    {
        public static float Clamp(CompYinAndMalevolent comp, float value) => MedicineRules.ControlledMalice(
            value, comp.MalevolentCapacity, comp.Zombie.health.hediffSet.HasHediff(RMDefOf.RM_HediffYakshaPearl),
            CompYinAndMalevolent.PearlMinMalevolent);
    }

    [HarmonyPatch(typeof(CompYinAndMalevolent), nameof(CompYinAndMalevolent.Malevolent), MethodType.Getter)]
    internal static class PurifyReadPatch
    {
        private static bool Prefix(CompYinAndMalevolent __instance, ref float ___malevolent, ref float __result)
        {
            if (!ZombieMedicineService.Active(__instance.Zombie, MXDefOf.MX_PurifyEffect)) return true;
            ___malevolent = PurifyPolicy.Clamp(__instance, ___malevolent);
            __result = ___malevolent;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompYinAndMalevolent), nameof(CompYinAndMalevolent.Malevolent), MethodType.Setter)]
    internal static class PurifyWritePatch
    {
        private static bool Prefix(CompYinAndMalevolent __instance, float value, ref float ___malevolent)
        {
            if (!ZombieMedicineService.Active(__instance.Zombie, MXDefOf.MX_PurifyEffect)) return true;
            float floor = PurifyPolicy.Clamp(__instance, 0f);
            ___malevolent = PurifyPolicy.Clamp(__instance, value);
            // Preserve native recovery when an external dispel crosses the active floor.
            // Capping excessive positive malice itself never cures a mental state.
            if (value < floor && __instance.Zombie.InMentalState) __instance.Zombie.MentalState.RecoverFromState();
            return false;
        }
    }

    [HarmonyPatch(typeof(CompYinAndMalevolent), nameof(CompYinAndMalevolent.Danger), MethodType.Getter)]
    internal static class PurifyBeforeDanger
    {
        private static void Prefix(CompYinAndMalevolent __instance)
        {
            if (ZombieMedicineService.Active(__instance.Zombie, MXDefOf.MX_PurifyEffect)) _ = __instance.Malevolent;
        }
    }
}
