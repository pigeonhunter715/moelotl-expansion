using RigorMortis;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    public sealed class Hediff_CapacityMedicine : Hediff_TimedMedicine
    {
        private bool warnedDay, warnedHour;
        public override void Tick()
        {
            base.Tick();
            if (pawn.Faction != Faction.OfPlayer || pawn.Dead || RemainingTicks <= 0) return;
            if (RemainingTicks > 60000) { warnedDay = warnedHour = false; return; }
            if (!warnedHour && RemainingTicks <= 2500) { warnedHour = warnedDay = true; Warn(); }
            else if (!warnedDay) { warnedDay = true; Warn(); }
        }
        private void Warn()
        {
            var comp = pawn.TryGetComp<CompYinAndMalevolent>();
            if (comp == null) return;
            float restored = comp.MalevolentCapacity / 1.5f;
            string risk = ZombieMedicineService.Active(pawn, MXDefOf.MX_PurifyEffect) ? "MX_CapacityPurifyRisk" :
                comp.Malevolent > restored ? "MX_CapacityDangerRisk" : "MX_CapacityNormalRisk";
            Messages.Message("MX_CapacityExpiring".Translate(pawn.LabelShort, (RemainingTicks / 2500f).ToString("0.0"), risk.Translate()),
                pawn, MessageTypeDefOf.CautionInput);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref warnedDay, "warnedDay");
            Scribe_Values.Look(ref warnedHour, "warnedHour");
        }
    }
}
