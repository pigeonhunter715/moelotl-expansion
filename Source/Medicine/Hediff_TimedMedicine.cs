using System;
using MoeLotlExpansion.Core;
using Verse;

namespace MoeLotlExpansion
{
    // Absolute game ticks keep expiry independent of pawn/map tick frequency.
    public class Hediff_TimedMedicine : Hediff
    {
        private long expiresAtTick;
        public long ExpiresAtTick => expiresAtTick;
        public int RemainingTicks => MedicineRules.Remaining(expiresAtTick, Find.TickManager.TicksGame);

        public override bool ShouldRemove => RemainingTicks <= 0 || base.ShouldRemove;
        public override HediffStage CurStage => RemainingTicks > 0 ? base.CurStage : null;
        protected override void OnStageIndexChanged(int stageIndex)
        {
            // PostMake sets severity before Renew assigns the expiry clock.
            // Vanilla assumes CurStage is non-null during that callback.
            if (CurStage != null) base.OnStageIndexChanged(stageIndex);
        }
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
