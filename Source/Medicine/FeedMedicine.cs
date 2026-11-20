using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoeLotlExpansion
{
    public sealed class CompTargetableMedicine : CompTargetable
    {
        protected override bool PlayerChoosesTarget => true;
        protected override TargetingParameters GetTargetingParameters() => new TargetingParameters
        {
            canTargetPawns = true, canTargetBuildings = false, canTargetItems = false,
            canTargetCorpses = false, mapObjectTargetsMustBeAutoAttackable = false
        };
        public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null) { yield return targetChosenByPlayer; }
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer) return false;
            AcceptanceReport report = MedicineService.CanUse(parent, pawn);
            if (!report.Accepted && showMessages) Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            return report.Accepted;
        }
    }

    public sealed class CompFeedMedicine : CompUsable
    {
        public override void TryStartUseJob(Pawn pawn, LocalTargetInfo extraTarget, bool forced = false)
        {
            if (!CanBeUsedBy(pawn, forced).Accepted) return;
            bool taiyin = TaiyinMedicine.IsTaiyin(parent.def);
            Pawn recipient = extraTarget.Pawn;
            if (recipient == null)
            {
                // The existing feed job targets another pawn. Taiyin additionally supports a
                // deliberate self-dose; other medicines keep their previous feed-only behavior.
                if (!taiyin) return;
                recipient = pawn;
            }
            if (recipient == null || recipient.Faction != Faction.OfPlayer) return;
            AcceptanceReport report = MedicineService.CanUse(parent, recipient);
            if (!report.Accepted)
            {
                if (taiyin)
                    Messages.Message(report.Reason, recipient, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!taiyin)
            {
                StartFeedJob(pawn, recipient);
                return;
            }

            // Both self-use and artificial feeding show the eligibility and the separate
            // transformation/death result before the one-pill action starts.
            Find.WindowStack.Add(new Dialog_MessageBox(
                TaiyinMedicine.ConfirmationText(recipient),
                "MX_TaiyinConfirm".Translate().ToString(),
                delegate { StartTaiyinUse(pawn, recipient); },
                "MX_TaiyinCancel".Translate().ToString(),
                null,
                "MX_TaiyinConfirmTitle".Translate().ToString()));
        }

        public override void UsedBy(Pawn pawn)
        {
            // Self-use runs through the native UseItem job, which revalidates here at the end.
            if (!TaiyinMedicine.IsTaiyin(parent.def)) return;
            AcceptanceReport report = MedicineService.TryUse(parent, pawn);
            if (!report.Accepted)
                Messages.Message(report.Reason, pawn, MessageTypeDefOf.RejectInput, false);
        }

        private void StartTaiyinUse(Pawn pawn, Pawn recipient)
        {
            if (pawn == null || recipient == null || pawn.Dead || recipient.Dead || recipient.Faction != Faction.OfPlayer) return;
            if (!CanBeUsedBy(pawn).Accepted) return;
            AcceptanceReport report = MedicineService.CanUse(parent, recipient);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, recipient, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (recipient == pawn)
            {
                JobDef useItem = DefDatabase<JobDef>.GetNamedSilentFail("UseItem");
                if (useItem == null)
                {
                    Log.Error("[MoeLotlExpansion] Missing UseItem JobDef for Taiyin self-use.");
                    return;
                }
                Job job = JobMaker.MakeJob(useItem, parent);
                job.count = 1;
                job.playerForced = true;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                return;
            }
            StartFeedJob(pawn, recipient);
        }

        private void StartFeedJob(Pawn pawn, Pawn recipient)
        {
            if (pawn == null || recipient == null || pawn.Dead || recipient.Dead) return;
            if (!CanBeUsedBy(pawn).Accepted) return;
            AcceptanceReport report = MedicineService.CanUse(parent, recipient);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, recipient, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Job job = JobMaker.MakeJob(MXDefOf.MX_FeedMedicine, recipient, parent);
            job.count = 1;
            job.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    public sealed class JobDriver_FeedMedicine : JobDriver
    {
        private Pawn Recipient => TargetA.Pawn;
        private Thing Medicine => pawn.carryTracker.CarriedThing ?? TargetB.Thing;
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed) &&
            pawn.Reserve(TargetB, job, 1, 1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Recipient == null || Recipient.Dead || !Recipient.Spawned ||
                Recipient.Faction != Faction.OfPlayer || !MedicineService.CanUse(Medicine, Recipient).Accepted ||
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil wait = Toils_General.Wait(120, TargetIndex.A);
            wait.FailOn(() => !pawn.Position.AdjacentTo8WayOrInside(Recipient.Position));
            wait.WithProgressBarToilDelay(TargetIndex.A);
            yield return wait;
            yield return Toils_General.Do(() =>
            {
                if (!pawn.Position.AdjacentTo8WayOrInside(Recipient.Position)) return;
                AcceptanceReport report = MedicineService.TryUse(Medicine, Recipient);
                if (!report.Accepted) Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, false);
            });
        }
    }
}
