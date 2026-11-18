using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using MoeLotlExpansion.Core;

namespace MoeLotlExpansion
{
    public static class PendantRestock
    {
        public static bool Safe(Pawn pawn) => pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed &&
            !pawn.Drafted && !pawn.InMentalState && pawn.Faction == Faction.OfPlayer && !PendantManager.Combat(pawn);
        public static Job FindJob(Pawn worker, MedicinePendant only = null, ThingDef manualType = null, int manualCount = 0)
        {
            if (!Safe(worker) || !worker.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return null;
            foreach (Pawn target in worker.Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer))
            {
                var pendant = MedicinePendant.Active(target);
                if (pendant == null || (only != null && pendant != only) || !Safe(target)) continue;
                var policy = PendantManager.Instance.For(target);
                if (manualType == null && !policy.autoRefill && !policy.manualRefill) continue;
                var inFlight = worker.Map.mapPawns.AllPawnsSpawned.Where(p => p.CurJob?.def == MXDefOf.MX_RestockPendant && p.CurJob.targetA.Thing == pendant).ToList();
                if (inFlight.Count > 0) continue; // One reserved transfer per pendant, no duplicate requests.
                var candidates = manualType != null ? new[] { manualType } : policy.entries.Where(e => e.target > 0)
                    .OrderByDescending(e => e.medicine.defName == "MX_RescuePill").Select(e => e.medicine);
                foreach (ThingDef medicine in candidates)
                {
                    var entry = policy.For(medicine); int current = pendant.Count(medicine);
                    if (current >= entry.target) entry.refillActive = false;
                    if (manualType == null && entry.target > 0 && (policy.manualRefill || current <= entry.target / 2)) entry.refillActive = true;
                    int need = manualType != null ? manualCount : MedicineRules.RefillNeed(entry.target, current, 0, entry.refillActive);
                    need = Math.Min(need, pendant.Space(medicine));
                    if (need <= 0) continue;
                    Thing source = worker.Map.listerThings.ThingsOfDef(medicine).Where(t => !t.IsForbidden(worker) &&
                        worker.CanReserveAndReach(t, PathEndMode.Touch, Danger.Some)).OrderBy(t => t.Position.DistanceToSquared(worker.Position)).FirstOrDefault();
                    if (source == null || !worker.CanReserve(target) || !worker.CanReach(target, PathEndMode.Touch, Danger.Some)) continue;
                    Job job = JobMaker.MakeJob(MXDefOf.MX_RestockPendant, pendant, source, target);
                    job.count = Math.Min(need, source.stackCount);
                    job.playerForced = manualType != null;
                    return job;
                }
                if (manualType == null && policy.manualRefill && policy.entries.All(e => pendant.Count(e.medicine) >= e.target)) policy.manualRefill = false;
            }
            return null;
        }
    }
    public sealed class WorkGiver_RestockPendant : WorkGiver
    {
        public override Job NonScanJob(Pawn pawn) => PendantRestock.FindJob(pawn);
    }
    public sealed class JobDriver_RestockPendant : JobDriver
    {
        private MedicinePendant Pendant => TargetA.Thing as MedicinePendant;
        private Pawn Recipient => TargetC.Pawn;
        private int Desired(Thing item)
        {
            if (Pendant == null || item == null) return 0;
            int wanted = job.playerForced ? job.count : PendantManager.Instance.For(Recipient).For(item.def).target - Pendant.Count(item.def);
            return Math.Max(0, Math.Min(Math.Min(wanted, item.stackCount), Pendant.Space(item.def)));
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(TargetC, job, 1, -1, null, errorOnFailed) && pawn.Reserve(TargetB, job, 1, job.count, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Pendant == null || Pendant.Destroyed || MedicinePendant.Active(Recipient) != Pendant ||
                !PendantRestock.Safe(pawn) || !PendantRestock.Safe(Recipient) ||
                (!job.playerForced && !PendantManager.Instance.For(Recipient).autoRefill && !PendantManager.Instance.For(Recipient).manualRefill));
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                job.count = Math.Min(job.count, Desired(TargetB.Thing));
                if (job.count <= 0) EndJobWith(JobCondition.Incompletable);
            });
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                Thing item = pawn.carryTracker.CarriedThing;
                int count = Desired(item);
                if (count > 0 && pawn.Position.AdjacentTo8WayOrInside(Recipient.Position)) Pendant.Load(item, count);
            });
        }
    }
}
