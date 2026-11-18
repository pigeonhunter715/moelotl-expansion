using System.Collections.Generic;
using System.Linq;
using Axolotl;
using RigorMortis;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    public static class MedicineCatalog
    {
        public const string Growth = "Axolotl_LotlqiPellet", Repair = "Axolotl_ReversalMeridianPill", Pearl = "Axolotl_YakshaPearl";
        public static bool Allowed(ThingDef def) => def != null && (def.GetModExtension<MedicineExtension>() != null ||
            def.defName == Growth || def.defName == Repair || def.defName == Pearl);
        public static IEnumerable<ThingDef> All => DefDatabase<ThingDef>.AllDefsListForReading.Where(Allowed);
        public static bool Maintainable(ThingDef def) => new[] { "MX_WarmingPill", "MX_CoolingPill", "MX_IndustryPill", "MX_DispelPill", "MX_CapacityPill", "MX_PurifyPill", Pearl }.Contains(def.defName);
        public static bool ManualOnly(ThingDef def) => def.defName == Repair || def.defName == "MX_ReleasePill";
        public static int Duration(Thing item) => item.def.defName == "MX_RescuePill" || item.def.defName == Pearl ? 120 :
            item.def.GetModExtension<MedicineExtension>() != null ? 100 : item.TryGetComp<CompUsable>().Props.useDuration;
        public static AcceptanceReport CanUse(Thing item, Pawn pawn)
        {
            if (item == null || item.Destroyed || item.stackCount <= 0 || pawn == null || pawn.Dead || !Allowed(item.def)) return false;
            if (item.def.GetModExtension<MedicineExtension>() != null) return MedicineService.CanUse(item, pawn);
            if (item.def.defName == Pearl)
                return pawn.Faction == Faction.OfPlayer && pawn.IsZombie(out _) && !pawn.InMentalState;
            // Native effects create map flecks; pause these two adapters while travelling.
            if (!pawn.Spawned) return "MX_NativeNeedsMap".Translate().ToString();
            if (pawn.IsMutant || !pawn.IsMoeLotl()) return false;
            return item.TryGetComp<CompUsable>()?.CanBeUsedBy(pawn, false, true) ?? (AcceptanceReport)false;
        }
        public static AcceptanceReport Use(Thing item, Pawn pawn)
        {
            var report = CanUse(item, pawn);
            if (!report.Accepted) return report;
            if (item.def.GetModExtension<MedicineExtension>() != null) return MedicineService.TryUse(item, pawn);
            if (item.def.defName == Pearl)
            {
                var effect = pawn.health.hediffSet.GetFirstHediffOfDef(RMDefOf.RM_HediffYakshaPearl);
                if (effect == null) effect = pawn.health.AddHediff(RMDefOf.RM_HediffYakshaPearl);
                var timer = effect.TryGetComp<HediffComp_Disappears>();
                if (timer == null) return false;
                timer.ticksToDisappear = (int)timer.Props.disappearsAfterTicks.Average;
                item.SplitOff(1).Destroy(DestroyMode.Vanish);
                return true;
            }
            // Keep native effect ordering including its single DestroySelf comp.
            int before = item.stackCount;
            item.TryGetComp<CompUsable>().UsedBy(pawn);
            return item.Destroyed || item.stackCount < before;
        }
    }
}
