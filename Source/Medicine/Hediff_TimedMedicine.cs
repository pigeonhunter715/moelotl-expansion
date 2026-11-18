using System;
using MoeLotlExpansion.Core;
using Verse;

namespace MoeLotlExpansion
{
    // Absolute game ticks keep expiry independent of pawn/map tick frequency.
    public sealed class Hediff_TimedMedicine : Hediff
    {
        private long expiresAtTick;
        public long ExpiresAtTick => expiresAtTick;
        public int RemainingTicks => MedicineRules.Remaining(expiresAtTick, Find.TickManager.TicksGame);

        public override bool ShouldRemove => RemainingTicks <= 0 || base.ShouldRemove;
        public override string LabelInBrackets => "MX_RemainingHours".Translate(
            (RemainingTicks / (float)MedicineRules.TicksPerHour).ToString("0.0"));

        public void Renew(int durationTicks)
        {
            expiresAtTick = MedicineRules.RenewAt(Find.TickManager.TicksGame, durationTicks);
            Severity = 1f;
        }

        public override bool TryMergeWith(Hediff other)
        {
            if (!(other is Hediff_TimedMedicine medicine) || other.def != def || other.Part != Part)
                return false;
            expiresAtTick = Math.Max(expiresAtTick, medicine.expiresAtTick);
            Severity = 1f;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expiresAtTick, "mxExpiresAtTick", 0L);
        }
    }
}
