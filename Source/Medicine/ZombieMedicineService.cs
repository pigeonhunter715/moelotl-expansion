using System.Linq;
using MoeLotlExpansion.Core;
using RigorMortis;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    [DefOf]
    public static class MXDefOf
    {
#pragma warning disable CS0649
        public static HediffDef MX_CapacityEffect, MX_PurifyEffect, MX_GatherEffect;
        public static HediffDef MX_DispelEffect;
        public static JobDef MX_FeedMedicine;
        public static JobDef MX_RestockPendant;
        public static JobDef MX_LoadPendantOne;
#pragma warning restore CS0649
        static MXDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(MXDefOf));
    }

    public static class ZombieMedicineService
    {
        public static bool Active(Pawn pawn, HediffDef def) => pawn?.health?.hediffSet.hediffs
            .OfType<Hediff_TimedMedicine>().Any(h => h.def == def && h.RemainingTicks > 0) == true;

        public static AcceptanceReport CanUse(Thing medicine, Pawn pawn, MedicineExtension config)
        {
            if (pawn?.health == null || pawn.Dead || !pawn.IsZombie(out _) || pawn.TryGetComp<CompYinAndMalevolent>() == null)
                return "MX_ZombieOnly".Translate().ToString();
            if (config.zombieMedicine == ZombieMedicine.Rescue)
                return pawn.Faction == Faction.OfPlayer && pawn.MentalState is MentalState_ZombieBerserk
                    ? (AcceptanceReport)true : "MX_RescueOnly".Translate().ToString();
            if (config.zombieMedicine == ZombieMedicine.Release)
                return Active(pawn, MXDefOf.MX_PurifyEffect) || pawn.health.hediffSet.HasHediff(RMDefOf.RM_HediffYakshaPearl)
                    ? (AcceptanceReport)true : "MX_NoBindings".Translate().ToString();
            if (config.effect == null || config.durationTicks <= 0 || !typeof(Hediff_TimedMedicine).IsAssignableFrom(config.effect.hediffClass))
                return "MX_InvalidMedicine".Translate().ToString();
            if (pawn.health.hediffSet.hediffs.Any(h => h.def == config.effect && !(h is Hediff_TimedMedicine)))
                return "MX_InvalidMedicine".Translate().ToString();
            if (config.zombieMedicine == ZombieMedicine.Gather)
            {
                if (Active(pawn, config.effect)) return "MX_GatherCooldown".Translate().ToString();
                if (GatherAmount(pawn) <= 0f) return "MX_MaliceFull".Translate().ToString();
            }
            return true;
        }

        public static float GatherAmount(Pawn pawn)
        {
            var comp = pawn.TryGetComp<CompYinAndMalevolent>();
            return MedicineRules.MaliceDose(comp.Malevolent, comp.MalevolentCapacity, Active(pawn, MXDefOf.MX_PurifyEffect));
        }

        public static void RemoveAll(Pawn pawn, HediffDef def)
        {
            foreach (Hediff effect in pawn.health.hediffSet.hediffs.Where(h => h.def == def).ToList())
                pawn.health.RemoveHediff(effect);
        }

        public static AcceptanceReport Apply(Thing medicine, Pawn pawn, MedicineExtension config)
        {
            var comp = pawn.TryGetComp<CompYinAndMalevolent>();
            if (config.zombieMedicine == ZombieMedicine.Release)
            {
                float current = comp.Malevolent;
                RemoveAll(pawn, MXDefOf.MX_PurifyEffect);
                RemoveAll(pawn, RMDefOf.RM_HediffYakshaPearl);
                comp.Malevolent = current;
            }
            else if (config.zombieMedicine == ZombieMedicine.Rescue)
            {
                RemoveAll(pawn, RMDefOf.RM_HediffYakshaPearl);
                comp.Malevolent = 0f;
                pawn.MentalState.RecoverFromState();
                pawn.jobs?.StopAll();
            }
            else
            {
                float addition = config.zombieMedicine == ZombieMedicine.Gather ? GatherAmount(pawn) : 0f;
                var effect = pawn.health.hediffSet.GetFirstHediffOfDef(config.effect) as Hediff_TimedMedicine;
                if (effect == null)
                {
                    effect = (Hediff_TimedMedicine)HediffMaker.MakeHediff(config.effect, pawn);
                    effect.Renew(config.durationTicks);
                    pawn.health.AddHediff(effect);
                }
                else { effect.Renew(config.durationTicks); pawn.health.Notify_HediffChanged(effect); }
                if (!Active(pawn, config.effect)) return "MX_InvalidMedicine".Translate().ToString();
                if (addition > 0f) comp.Malevolent += addition;
                // Getter enforces dynamic purify cap before subsequent danger checks.
                _ = comp.Malevolent;
            }
            medicine.SplitOff(1).Destroy(DestroyMode.Vanish);
            PendantManager.Instance?.MedicineUsed(pawn, config.zombieMedicine);
            return true;
        }
    }
}
