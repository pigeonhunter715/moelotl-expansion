using System.Collections.Generic;
using Axolotl;
using RigorMortis;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    public struct TaiyinEligibility
    {
        public bool PlayerFaction;
        public bool Axolotl;
        public bool Flying;
        public bool Awoken;
        public bool Berserk;
        public bool Meridians;
        public bool Kind;
        public int Kills;
        public float MeridianSeverity;

        public bool KillCountReached => Kills >= TaiyinMedicine.RequiredKills;
        public bool Eligible => PlayerFaction && Axolotl && Flying && Awoken && !Berserk &&
            Meridians && Kind && KillCountReached;
    }

    // Taiyin is deliberately outside the ordinary timed zombie medicines: taking a dose is always
    // possible for a living humanlike, while the result at the end of the action is either a
    // permanent transformation or a normal death.
    public static class TaiyinMedicine
    {
        public const string PillDefName = "MX_TaiyinPill";
        public const string PreparationDefName = "MX_TaiyinPreparation";
        public const string EffectDefName = "MX_TaiyinEffect";
        public const string KillRecordDefName = "MX_TaiyinZombieHumanlikeKills";
        public const string FlyingZombieDefName = "RM_FlyingZombie";
        public const string DeathMeridiansDefName = "RM_DeathMeridians";
        public const int RequiredKills = 1000;
        public const float RequiredMeridianSeverity = 5f;

        public static HediffDef EffectDef => DefDatabase<HediffDef>.GetNamedSilentFail(EffectDefName);
        public static RecordDef KillRecord => DefDatabase<RecordDef>.GetNamedSilentFail(KillRecordDefName);
        public static TraitDef FallbackPerfectMemory => DefDatabase<TraitDef>.GetNamedSilentFail("MX_PerfectMemory");

        public static bool IsTaiyin(ThingDef def) => def != null && def.defName == PillDefName;

        public static AcceptanceReport CanTake(Thing medicine, Pawn pawn)
        {
            if (medicine == null || medicine.Destroyed || medicine.stackCount < 1)
                return "MX_NoMedicine".Translate().ToString();
            HediffDef effect = EffectDef;
            if (effect == null)
                return "MX_InvalidMedicine".Translate().ToString();
            if (pawn == null || pawn.Destroyed || pawn.health == null || pawn.Dead ||
                pawn.RaceProps?.Humanlike != true)
                return "MX_TaiyinLivingHumanlikeOnly".Translate().ToString();
            if (pawn.health.hediffSet.HasHediff(effect))
                return "MX_TaiyinAlreadyTransformed".Translate().ToString();
            return true;
        }

        // Cheap enough for per-tick repair/danger patches. It never reads dynamic stats.
        public static bool Active(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.health == null) return false;
            HediffDef effect = EffectDef;
            if (effect == null) return false;
            if (!(pawn.health.hediffSet.GetFirstHediffOfDef(effect) is Hediff_Taiyin)) return false;
            return !Paused(pawn);
        }

        // Once the transformation succeeds, only the three identity conditions and death pause it.
        // The kill record is intentionally not re-checked after success.
        public static bool Paused(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return true;
            if (!pawn.IsMutant || pawn.mutant?.Def?.defName != FlyingZombieDefName) return true;
            CompYinAndMalevolent comp = pawn.TryGetComp<CompYinAndMalevolent>();
            if (comp == null || !comp.Awoken) return true;
            return TraitDefOf.Kind == null ||
                pawn.story?.traits?.HasTrait(TraitDefOf.Kind) != true;
        }

        public static TaiyinEligibility Evaluate(Pawn pawn)
        {
            var result = new TaiyinEligibility();
            if (pawn == null || pawn.Dead || pawn.health == null) return result;

            result.PlayerFaction = pawn.Faction == Faction.OfPlayer;
            result.Axolotl = pawn.IsMoeLotl();
            result.Flying = pawn.IsMutant && pawn.mutant?.Def?.defName == FlyingZombieDefName;
            CompYinAndMalevolent comp = pawn.TryGetComp<CompYinAndMalevolent>();
            result.Awoken = comp != null && comp.Awoken;
            result.Berserk = pawn.MentalState is MentalState_ZombieBerserk;
            result.Kind = TraitDefOf.Kind != null &&
                pawn.story?.traits?.HasTrait(TraitDefOf.Kind) == true;

            HediffDef meridiansDef = DefDatabase<HediffDef>.GetNamedSilentFail(DeathMeridiansDefName);
            Hediff meridians = meridiansDef == null
                ? null
                : pawn.health.hediffSet.GetFirstHediffOfDef(meridiansDef);
            result.MeridianSeverity = meridians?.Severity ?? 0f;
            result.Meridians = result.MeridianSeverity >= RequiredMeridianSeverity;
            result.Kills = KillCount(pawn);
            return result;
        }

        public static int KillCount(Pawn pawn)
        {
            if (pawn?.records == null) return 0;
            RecordDef record = KillRecord;
            return record == null ? 0 : pawn.records.GetAsInt(record);
        }

        public static List<string> ReasonLines(TaiyinEligibility eligibility)
        {
            var lines = new List<string>();
            if (!eligibility.PlayerFaction) lines.Add("MX_TaiyinReasonNotPlayer".Translate().ToString());
            if (!eligibility.Axolotl) lines.Add("MX_TaiyinReasonNotAxolotl".Translate().ToString());
            if (!eligibility.Flying) lines.Add("MX_TaiyinReasonNotFlying".Translate().ToString());
            if (!eligibility.Awoken) lines.Add("MX_TaiyinReasonNotAwoken".Translate().ToString());
            if (eligibility.Berserk) lines.Add("MX_TaiyinReasonBerserk".Translate().ToString());
            if (!eligibility.Meridians)
                lines.Add("MX_TaiyinReasonMeridians".Translate(
                    eligibility.MeridianSeverity.ToString("0.#")).ToString());
            if (!eligibility.Kind) lines.Add("MX_TaiyinReasonNotKind".Translate().ToString());
            if (!eligibility.KillCountReached) lines.Add("MX_TaiyinReasonKills".Translate().ToString());
            return lines;
        }

        // Used by the manual order confirmation so eligibility and the resulting death or
        // transformation are shown separately before one pill is committed.
        public static string ConfirmationText(Pawn pawn)
        {
            TaiyinEligibility eligibility = Evaluate(pawn);
            var lines = new List<string>();
            if (eligibility.Eligible)
                lines.Add("MX_TaiyinEligible".Translate().ToString());
            else
                lines.AddRange(ReasonLines(eligibility));
            lines.Add("MX_TaiyinKillsStatus".Translate(eligibility.Kills, RequiredKills).ToString());
            string result = eligibility.Eligible
                ? "MX_TaiyinResultImmortal".Translate().ToString()
                : "MX_TaiyinResultDeath".Translate().ToString();
            string target = pawn?.LabelShortCap ?? "?";
            return "MX_TaiyinConfirmText".Translate(target, string.Join("\n", lines), result).ToString();
        }

        public static AcceptanceReport Apply(Thing medicine, Pawn pawn)
        {
            // Revalidate availability and the one-success rule at the final action.
            AcceptanceReport report = CanTake(medicine, pawn);
            if (!report.Accepted) return report;
            HediffDef effectDef = EffectDef;
            if (effectDef == null) return "MX_InvalidMedicine".Translate().ToString();

            // Eligibility is evaluated at the end of the action. Losing or gaining a condition
            // during the 120-tick use therefore changes only the result, never the cost.
            TaiyinEligibility eligibility = Evaluate(pawn);

            // A lethal failure consumes exactly one dose before any death handling.
            medicine.SplitOff(1).Destroy(DestroyMode.Vanish);

            if (!eligibility.Eligible)
            {
                Messages.Message("MX_TaiyinLethalMessage".Translate(pawn.LabelShortCap), pawn,
                    MessageTypeDefOf.NegativeEvent, false);
                TaiyinLethalDose.Kill(pawn);
                if (!pawn.Dead)
                    Log.Error("[MoeLotlExpansion] Taiyin lethal dose did not kill " + pawn);
                return true;
            }

            if (!pawn.health.hediffSet.HasHediff(effectDef))
            {
                Hediff effect = HediffMaker.MakeHediff(effectDef, pawn);
                effect.Severity = 1f;
                pawn.health.AddHediff(effect);
            }
            GrantTraits(pawn);
            Messages.Message("MX_TaiyinSuccessMessage".Translate(pawn.LabelShortCap), pawn,
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public static void GrantTraits(Pawn pawn)
        {
            TraitSet traits = pawn?.story?.traits;
            if (traits == null) return;

            TraitDef beautyDef = DefDatabase<TraitDef>.GetNamed("Beauty");
            Trait beauty = traits.GetTrait(beautyDef);
            if (beauty == null || beauty.Degree != 2)
            {
                if (beauty != null) traits.RemoveTrait(beauty);
                traits.GainTrait(new Trait(beautyDef, 2));
            }

            // Anomaly's own PerfectMemory already stops SkillRecord.Interval decay. Without
            // Anomaly, the extension trait of the same name/label is used by a scoped prefix.
            TraitDef perfect = ModsConfig.AnomalyActive
                ? DefDatabase<TraitDef>.GetNamedSilentFail("PerfectMemory")
                : null;
            TraitDef fallback = FallbackPerfectMemory;
            if (perfect != null)
            {
                if (!traits.HasTrait(perfect) && (fallback == null || !traits.HasTrait(fallback)))
                    traits.GainTrait(new Trait(perfect, 0));
                return;
            }
            if (fallback != null && !traits.HasTrait(fallback))
                traits.GainTrait(new Trait(fallback, 0));
        }

        public static float ScaleRepair(float amount, Pawn user) =>
            Active(user) ? amount * 3f : amount;

        public static int RepairStoppingTicks(CompZombieBodyRepair comp) =>
            Active(comp.GetPawn) ? 600 : 1800;
    }

    public class Hediff_Taiyin : Hediff
    {
        public override bool ShouldRemove => false;
        public override HediffStage CurStage =>
            TaiyinMedicine.Paused(pawn) ? null : base.CurStage;
        public override bool TryMergeWith(Hediff other) => false;
    }
}
