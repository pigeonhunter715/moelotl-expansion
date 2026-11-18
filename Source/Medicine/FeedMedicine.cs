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
            if (!CanBeUsedBy(pawn, forced).Accepted || extraTarget.Pawn == null ||
                extraTarget.Pawn.Faction != Faction.OfPlayer || !MedicineService.CanUse(parent, extraTarget.Pawn).Accepted) return;
            Job job = JobMaker.MakeJob(MXDefOf.MX_FeedMedicine, extraTarget, parent);
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
