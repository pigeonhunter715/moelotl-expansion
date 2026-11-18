using System.Collections.Generic;
using System.Linq;
using Axolotl;
using HarmonyLib;
using Verse;

namespace MoeLotlExpansion
{
    public sealed class AlchemyRecipeExtension : DefModExtension
    {
        public List<ResearchProjectDef> researches = new List<ResearchProjectDef>();
        public bool Unlocked => researches.All(r => r.IsFinished);
    }

    [StaticConstructorOnStartup]
    internal static class Bootstrap
    {
        static Bootstrap() => new Harmony("pigeonhunter715.moelotl.expansion").PatchAll(typeof(Bootstrap).Assembly);
    }

    [HarmonyPatch(typeof(Building_AlchemyStove_Base), nameof(Building_AlchemyStove_Base.GetPillCanRefine), MethodType.Getter)]
    internal static class AlchemyAvailablePills
    {
        private static readonly List<ThingDef> Recipes = DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.GetModExtension<AlchemyRecipeExtension>() != null).ToList();

        private static void Postfix(ref List<ThingDef> __result)
        {
            __result = new List<ThingDef>(__result);
            foreach (ThingDef pill in Recipes)
                if (pill.GetModExtension<AlchemyRecipeExtension>().Unlocked && !__result.Contains(pill))
                    __result.Add(pill);
        }
    }

    [HarmonyPatch(typeof(Building_AlchemyStove_Base), nameof(Building_AlchemyStove_Base.CurrentTargetPill_StuffCountMax), MethodType.Getter)]
    internal static class AlchemyWholeDose
    {
        private static void Postfix(Building_AlchemyStove_Base __instance, ref int __result)
        {
            // Native easy-alchemy truncates 1 / 2 to 0. A prepared batch is indivisible.
            if (__instance.TargetPill?.GetModExtension<AlchemyRecipeExtension>() != null)
                __result = 1;
        }
    }
}
