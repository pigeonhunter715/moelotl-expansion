using Axolotl;
using MoeLotlExpansion.Core;
using RigorMortis;
using Verse;

namespace MoeLotlExpansion
{
    public static class MedicineService
    {
        public static AcceptanceReport CanUse(Thing medicine, Pawn target)
        {
            if (medicine == null || medicine.Destroyed || medicine.stackCount < 1)
                return "MX_NoMedicine".Translate().ToString();
            MedicineExtension config = medicine.def.GetModExtension<MedicineExtension>();
            if (config?.effect == null || config.durationTicks <= 0 ||
                config.effect.hediffClass != typeof(Hediff_TimedMedicine))
                return "MX_InvalidMedicine".Translate().ToString();

            bool exists = target?.health != null;
            MedicineRejection rejection = MedicineRules.CheckLivingTarget(
                exists, exists && !target.Dead, exists && target.IsMoeLotl(),
                exists && target.IsZombie(out _));
            if (rejection == MedicineRejection.InvalidTarget)
                return "MX_InvalidTarget".Translate().ToString();
            if (rejection == MedicineRejection.RequiresLivingMoeLotl)
                return "MX_LivingMoeLotlOnly".Translate().ToString();

            // Fail safely if another mod supplies an incompatible instance for this Def.
            foreach (Hediff existing in target.health.hediffSet.hediffs)
                if (existing.def == config.effect && !(existing is Hediff_TimedMedicine))
                    return "MX_InvalidMedicine".Translate().ToString();
            return true;
        }

        public static AcceptanceReport TryUse(Thing medicine, Pawn target)
        {
            AcceptanceReport report = CanUse(medicine, target);
            if (!report.Accepted) return report;
            MedicineExtension config = medicine.def.GetModExtension<MedicineExtension>();
            var existing = target.health.hediffSet.GetFirstHediffOfDef(config.effect) as Hediff_TimedMedicine;
            if (existing != null)
            {
                existing.Renew(config.durationTicks);
                target.health.Notify_HediffChanged(existing);
            }
            else
            {
                var effect = (Hediff_TimedMedicine)HediffMaker.MakeHediff(config.effect, target);
                effect.Renew(config.durationTicks);
                target.health.AddHediff(effect);
            }
            var applied = target.health.hediffSet.GetFirstHediffOfDef(config.effect) as Hediff_TimedMedicine;
            if (applied == null || applied.RemainingTicks <= 0)
                return "MX_InvalidMedicine".Translate().ToString();

            // One synchronous effect-and-consumption path; no second DestroySelf comp.
            medicine.SplitOff(1).Destroy(DestroyMode.Vanish);
            return true;
        }
    }
}
