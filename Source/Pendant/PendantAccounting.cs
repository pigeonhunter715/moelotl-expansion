using HarmonyLib;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    // Bypass the immutable value cache only for this dynamic container. Native mass
    // already reads the pendant's top-level Mass, so only market value needs this.
    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValue), new[] { typeof(Thing), typeof(bool), typeof(int) })]
    internal static class PendantContentsStats
    {
        private static bool Prefix(StatWorker __instance, Thing thing, bool applyPostProcess, StatDef ___stat, ref float __result)
        {
            if (!(thing is MedicinePendant) || ___stat != StatDefOf.MarketValue) return true;
            __result = __instance.GetValue(StatRequest.For(thing), applyPostProcess);
            return false;
        }
    }
    // Covers inspection, trading and wealth with a single final addition.
    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValue), new[] { typeof(StatRequest), typeof(bool) })]
    internal static class PendantContentsRequestStats
    {
        private static void Postfix(StatRequest req, StatDef ___stat, ref float __result)
        {
            if (!(req.Thing is MedicinePendant pendant)) return;
            if (___stat == StatDefOf.MarketValue) __result += pendant.ContentsValue;
        }
    }
    // Wealth traversal counts this container's summed market value once.
    [HarmonyPatch(typeof(WealthWatcher), nameof(WealthWatcher.WealthItemsFilter))]
    internal static class PendantWealthOnce
    {
        private static void Postfix(IThingHolder x, ref bool __result)
        { if (x is MedicinePendant) __result = false; }
    }
}
