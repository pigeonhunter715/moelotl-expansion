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

        private static IEnumerable<ThingDef> Candidates(PawnMedicinePolicy policy, ThingDef manualType)
        {
            if (manualType != null) { yield return manualType; yield break; }
            foreach (ThingDef medicine in policy.entries.Where(e => e.target > 0)
                .OrderByDescending(e => e.medicine.defName == "MX_RescuePill").Select(e => e.medicine))
                yield return medicine;
        }

        private static int ComputedNeed(PawnMedicinePolicy policy, MedicinePendant pendant, MedicinePolicy entry,
            int current, int manualCount, bool manual, bool commit)
        {
            bool refillActive = entry.refillActive;
            if (current >= entry.target) refillActive = false;
            if (!manual && entry.target > 0 && (policy.manualRefill || current <= entry.target / 2)) refillActive = true;
            if (commit) entry.refillActive = refillActive;
            int need = manual ? manualCount : MedicineRules.RefillNeed(entry.target, current, 0, refillActive);
            return Math.Min(need, pendant.Space(entry.medicine));
        }

        // Single source resolver for scheduling and for the wearer deferral check, so both
        // paths apply the same forbidden/reservation/reach and capacity rules.
        private static Thing FindSource(Pawn worker, ThingDef medicine) => worker.Map.listerThings.ThingsOfDef(medicine)
            .Where(t => !t.IsForbidden(worker) && worker.CanReserveAndReach(t, PathEndMode.Touch, Danger.Some))
            .OrderBy(t => t.Position.DistanceToSquared(worker.Position)).FirstOrDefault();

        private static Thing FindSupply(Pawn worker, MedicinePendant pendant, PawnMedicinePolicy policy,
            ThingDef manualType, int manualCount, bool updatePolicy, out int count)
        {
            Pawn recipient = pendant.Wearer;
            count = 0;
            foreach (ThingDef medicine in Candidates(policy, manualType))
            {
                MedicinePolicy entry = policy.For(medicine);
                int need = ComputedNeed(policy, pendant, entry, pendant.Count(medicine),
                    manualCount, manualType != null, updatePolicy);
                if (need <= 0) continue;
                Thing source = FindSource(worker, medicine);
                if (source == null) continue;
                if (worker != recipient && (!worker.CanReserve(recipient) ||
                    !worker.CanReach(recipient, PathEndMode.Touch, Danger.Some))) continue;
                count = Math.Min(need, source.stackCount);
                return source;
            }
            return null;
        }

        private static bool CanSelfRestock(Pawn wearer, MedicinePendant pendant, PawnMedicinePolicy policy)
        {
            return wearer.Awake() && wearer.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                wearer.CurJob?.playerForced != true && (wearer.IsColonist || wearer.IsColonySubhuman) &&
                wearer.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true &&
                !wearer.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) &&
                FindSupply(wearer, pendant, policy, null, 0, false, out _) != null;
        }

        private static bool TargetsPendant(Job job, MedicinePendant pendant) =>
            job != null && (job.def == MXDefOf.MX_RestockPendant || job.def == MXDefOf.MX_LoadPendantOne) &&
            job.targetA.Thing == pendant;

        private static bool HasTransfer(Pawn pawn, MedicinePendant pendant)
        {
            if (TargetsPendant(pawn.CurJob, pendant)) return true;
            if (pawn.jobs?.jobQueue == null) return false;
            foreach (QueuedJob queued in pawn.jobs.jobQueue)
                if (TargetsPendant(queued?.job, pendant)) return true;
            return false;
        }

        public static Job FindJob(Pawn worker, MedicinePendant only = null, ThingDef manualType = null, int manualCount = 0)
        {
            if (!Safe(worker) || !worker.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return null;
            Map map = worker.Map;
            if (map == null) return null;

            // Own pendant first so a wearer does not lose the self-haul to a nearby helper,
            // then the existing wearer scan order.
            var wearers = new List<Pawn>();
            MedicinePendant ownPendant = MedicinePendant.Active(worker);
            if (ownPendant != null && (only == null || ownPendant == only)) wearers.Add(worker);
            wearers.AddRange(map.mapPawns.AllPawnsSpawned.Where(p => p != worker && p.Faction == Faction.OfPlayer));

            foreach (Pawn recipient in wearers)
            {
                MedicinePendant pendant = MedicinePendant.Active(recipient);
                if (pendant == null || (only != null && pendant != only) || !Safe(recipient) || recipient.Map != map) continue;
                var policy = PendantManager.Instance?.For(recipient);
                if (policy == null) continue;
                if (manualType == null && !policy.autoRefill && !policy.manualRefill) continue;
                if (map.mapPawns.AllPawnsSpawned.Any(p => HasTransfer(p, pendant))) continue;
                if (manualType == null && recipient != worker && CanSelfRestock(recipient, pendant, policy)) continue;

                Thing source = FindSupply(worker, pendant, policy, manualType, manualCount, true, out int count);
                if (source != null)
                {
                    Job job = JobMaker.MakeJob(manualType == null ? MXDefOf.MX_RestockPendant : MXDefOf.MX_LoadPendantOne,
                        pendant, source, recipient);
                    job.count = count;
                    job.playerForced = manualType != null;
                    return job;
                }
                if (manualType == null && policy.manualRefill && policy.entries.All(e => pendant.Count(e.medicine) >= e.target)) policy.manualRefill = false;
            }
            return null;
        }

        // Wearer-first manual single-dose order. A manual order intentionally does not require
        // automatic refill or Hauling work; only execution prerequisites are checked.
        public static bool TryOrderLoadOne(MedicinePendant pendant, ThingDef medicine)
        {
            if (pendant == null || medicine == null) return false;
            Pawn wearer = pendant.Wearer;
            if (OrderLoadOne(wearer, pendant, medicine)) return true;
            Map map = wearer?.Map ?? pendant.Map;
            if (map == null) return false;
            foreach (Pawn worker in map.mapPawns.FreeColonistsSpawned)
            {
                if (worker == wearer) continue;
                if (OrderLoadOne(worker, pendant, medicine)) return true;
            }
            return false;
        }

        private static bool OrderLoadOne(Pawn worker, MedicinePendant pendant, ThingDef medicine)
        {
            if (worker?.jobs == null) return false;
            Job job = FindJob(worker, pendant, medicine, 1);
            return job != null && worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
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
        // Ordered jobs also set playerForced=true, including refill-to-target jobs.
        // A separate saved JobDef is the explicit one-dose request.
        private bool LoadOne => job.def == MXDefOf.MX_LoadPendantOne;
        private bool SelfLoad => pawn == Recipient;
        private int Desired(Thing item)
        {
            if (Pendant == null || item == null) return 0;
            int wanted = LoadOne ? 1 : PendantManager.Instance.For(Recipient).For(item.def).target - Pendant.Count(item.def);
            return Math.Max(0, Math.Min(Math.Min(wanted, item.stackCount), Pendant.Space(item.def)));
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            (SelfLoad || pawn.Reserve(TargetC, job, 1, -1, null, errorOnFailed)) &&
            pawn.Reserve(TargetB, job, 1, job.count, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Pendant == null || Pendant.Destroyed || MedicinePendant.Active(Recipient) != Pendant ||
                !PendantRestock.Safe(pawn) || !PendantRestock.Safe(Recipient) ||
                (!LoadOne && !PendantManager.Instance.For(Recipient).autoRefill && !PendantManager.Instance.For(Recipient).manualRefill));
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                job.count = Math.Min(job.count, Desired(TargetB.Thing));
                if (job.count <= 0) EndJobWith(JobCondition.Incompletable);
            });
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);
            // Keep the toil count and order stable for old save jobs, which address toils by
            // index. A self-load has no separate recipient, so the return-to-recipient step is
            // a no-op at this same index instead of being removed.
            yield return SelfLoad ? Toils_General.Do(() => { }) : Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                Thing item = pawn.carryTracker.CarriedThing;
                int count = Desired(item);
                if (count > 0 && (SelfLoad || pawn.Position.AdjacentTo8WayOrInside(Recipient.Position))) Pendant.Load(item, count);
            });
        }
    }
}
