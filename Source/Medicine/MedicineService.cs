using Axolotl;
using MoeLotlExpansion.Core;
using RigorMortis;
using Verse;
using System.Linq;

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
                !typeof(Hediff_TimedMedicine).IsAssignableFrom(config.effect.hediffClass))
                return "MX_InvalidMedicine".Translate().ToString();

            bool exists = target?.health != null;
            MedicineRejection rejection = MedicineRules.CheckLivingTarget(
                exists, exists && !target.Dead, exists && target.IsMoeLotl(),
                exists && target.IsZombie(out _));
            if (rejection == MedicineRejection.InvalidTarget)
                return "MX_InvalidTarget".Translate().ToString();
            if (rejection == MedicineRejection.RequiresLivingMoeLotl)
                return "MX_LivingMoeLotlOnly".Translate().ToString();
            if (config.requiresMeridians)
            {
                var energy = target.TryGetComp<CompAxolotlEnergy>();
                var part = target.RaceProps.body.AllParts.FirstOrDefault(p => p.def == AxolotlBodyPartDefOf.Axolotl_Meridians);
                if (target.IsMutant || energy == null || energy.Level < 1 || part == null ||
                    target.health.hediffSet.GetPartHealth(part) <= 0f)
                    return "MX_RequiresMeridians".Translate().ToString();
            }

            // Fail safely if another mod supplies an incompatible instance for this Def.
            foreach (Hediff existing in target.health.hediffSet.hediffs)
                if (existing.def == config.effect && !(existing is Hediff_TimedMedicine))
                    return "MX_InvalidMedicine".Translate().ToString();
            var timed = target.health.hediffSet.GetFirstHediffOfDef(config.effect) as Hediff_TimedMedicine;
            if (config.preventRefresh && timed != null && timed.RemainingTicks > 0)
                return "MX_MedicineCooldown".Translate((timed.RemainingTicks / 2500f).ToString("0.0")).ToString();
            if (config.restoresQi && RecoveryFor(target) <= 0f)
                return "MX_QiFull".Translate().ToString();
            return true;
        }

        public static float RecoveryFor(Pawn target)
        {
            var energy = target?.TryGetComp<CompAxolotlEnergy>();
            return energy == null ? 0f : MedicineRules.RecoveryAmount(energy.EnergyMax, energy.Energy);
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
            if (config.restoresQi)
            {
                var energy = target.TryGetComp<CompAxolotlEnergy>();
                energy.Energy += RecoveryFor(target);
            }

            // One synchronous effect-and-consumption path; no second DestroySelf comp.
            medicine.SplitOff(1).Destroy(DestroyMode.Vanish);
            return true;
        }
    }
}
