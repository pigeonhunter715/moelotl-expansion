using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    public sealed class CompUsableMedicine : CompUsable
    {
        public override AcceptanceReport CanBeUsedBy(Pawn pawn, bool forced = false, bool ignoreReserveAndReachable = false)
        {
            AcceptanceReport report = MedicineService.CanUse(parent, pawn);
            if (!report.Accepted) return report;
            return base.CanBeUsedBy(pawn, forced, ignoreReserveAndReachable);
        }

        public override void UsedBy(Pawn pawn)
        {
            // Base JobDriver_UseItem waits 100 ticks; revalidate before changing anything.
            AcceptanceReport report = CanBeUsedBy(pawn, false, true);
            if (report.Accepted) report = MedicineService.TryUse(parent, pawn);
            if (!report.Accepted)
                Messages.Message(report.Reason, pawn, MessageTypeDefOf.RejectInput, false);
        }
    }
}
