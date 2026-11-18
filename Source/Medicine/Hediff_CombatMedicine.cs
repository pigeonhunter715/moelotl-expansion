using MoeLotlExpansion.Core;
using Verse;

namespace MoeLotlExpansion
{
    // One saved clock covers 8h combat plus 6h fatigue, including off-map time.
    public sealed class Hediff_CombatMedicine : Hediff_TimedMedicine
    {
        private bool wasFatigued;
        public bool IsFatigued => RemainingTicks <= 6 * MedicineRules.TicksPerHour;
        public override HediffStage CurStage => RemainingTicks <= 0 ? null : def.stages[IsFatigued ? 1 : 0];
        public override string LabelBase => IsFatigued ? "MX_CombatFatigue".Translate().ToString() : base.LabelBase;
        public override string LabelInBrackets => "MX_RemainingHours".Translate(
            ((IsFatigued ? RemainingTicks : RemainingTicks - 6 * MedicineRules.TicksPerHour) / 2500f).ToString("0.0"));

        public override void Tick()
        {
            base.Tick();
            if (wasFatigued == IsFatigued) return;
            wasFatigued = IsFatigued;
            pawn.health.Notify_HediffChanged(this);
        }
    }
}
