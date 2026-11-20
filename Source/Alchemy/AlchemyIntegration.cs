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
        public bool industrialOnly;
        public int fixedActiveTicks;

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

        private static void Postfix(Building_AlchemyStove_Base __instance, ref List<ThingDef> __result)
        {
            bool industrial = __instance is Building_AlchemyStove_Industrial;
            __result = new List<ThingDef>(__result);
            __result.RemoveAll(pill =>
            {
                AlchemyRecipeExtension existing = pill.GetModExtension<AlchemyRecipeExtension>();
                return existing != null && existing.industrialOnly && !industrial;
            });
            foreach (ThingDef pill in Recipes)
            {
                AlchemyRecipeExtension config = pill.GetModExtension<AlchemyRecipeExtension>();
                if (!config.Unlocked) continue;
                if (config.industrialOnly && !industrial) continue;
                if (!__result.Contains(pill)) __result.Add(pill);
            }
        }
    }

    [HarmonyPatch(typeof(Building_AlchemyStove_Base), nameof(Building_AlchemyStove_Base.CurrentTargetPill_StuffCountMax), MethodType.Getter)]
    internal static class AlchemyWholeDose
    {
        private static void Postfix(Building_AlchemyStove_Base __instance, ref int __result)
        {
            ThingDef pill = __instance.TargetPill;
            if (pill?.GetModExtension<AlchemyRecipeExtension>() == null) return;
            CompProperties_PillStuffNeed need = pill.GetCompProperties<CompProperties_PillStuffNeed>();
            if (need != null) __result = need.StuffCount;
        }
    }
}
