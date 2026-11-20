using System.Collections.Generic;
using System.Linq;
using Axolotl;
using RigorMortis;
using RimWorld;
using RimWorld.Planet;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace MoeLotlExpansion
{
    public sealed class PendantManager : GameComponent
    {
        private List<PawnMedicinePolicy> policies = new List<PawnMedicinePolicy>();
        public PendantManager(Game game) { }
        public static PendantManager Instance => Current.Game?.GetComponent<PendantManager>();
        public IEnumerable<PawnMedicinePolicy> Policies => policies;
        public static bool Available(Pawn pawn) => pawn != null && (pawn.Spawned || pawn.IsCaravanMember() ||
            // Jumping temporarily despawns the pawn into a map flyer, not storage.
            (pawn.ParentHolder is PawnFlyer flyer && flyer.Spawned && flyer.FlyingPawn == pawn));
        public PawnMedicinePolicy For(Pawn pawn)
        {
            var policy = policies.FirstOrDefault(p => p.pawn == pawn);
            if (policy == null) { policy = new PawnMedicinePolicy { pawn = pawn }; policies.Add(policy); }
            return policy;
        }
        public void MedicineUsed(Pawn pawn, ZombieMedicine medicine)
        {
            if (medicine != ZombieMedicine.Release && medicine != ZombieMedicine.Rescue) return;
            var policy = For(pawn);
            if (medicine == ZombieMedicine.Release)
            {
                foreach (var e in policy.entries.Where(e => e.medicine.defName == "MX_PurifyPill" || e.medicine.defName == MedicineCatalog.Pearl)) e.mode = AutoMedicineMode.Off;
            }
            else
            {
                policy.gatherPausedUntil = Find.TickManager.TicksGame + 2500;
                policy.pearlPaused = true;
            }
        }
        public AcceptanceReport Start(MedicinePendant pendant, Thing item, bool manual)
        {
            Pawn pawn = pendant.Wearer;
            if (pawn == null || pawn.Dead || !Available(pawn) || pawn.Faction != Faction.OfPlayer || MedicinePendant.Active(pawn) != pendant || !pendant.Contents.Contains(item)) return false;
            var report = MedicineCatalog.CanUse(item, pawn);
            if (!report.Accepted) return report;
            var policy = For(pawn);
            bool emergency = item.def.defName == "MX_RescuePill";
            if (!emergency && Find.TickManager.TicksGame < policy.nextOrdinaryTick) return "MX_DeliveryGap".Translate().ToString();
            policy.CancelDelivery();
            policy.pendingPendant = pendant; policy.pendingMedicine = item; policy.pendingManual = manual;
            policy.deliveryAt = Find.TickManager.TicksGame + MedicineCatalog.Duration(item);
            if (!manual && policy.For(item.def).mode == AutoMedicineMode.Maintain)
                policy.deliveryAt = System.Math.Max(policy.deliveryAt, Find.TickManager.TicksGame + EffectRemaining(pawn, item.def));
            return true;
        }
        // Runs at the start of the normal tick list, before pawn health/capacity checks.
        public void ProcessTick()
        {
            int now = Find.TickManager.TicksGame;
            foreach (var policy in policies.ToArray())
            {
                Pawn pawn = policy.pawn;
                var pendant = MedicinePendant.Active(pawn);
                if (pawn == null || pawn.Dead || !Available(pawn) || pawn.Faction != Faction.OfPlayer || pendant == null) { policy.CancelDelivery(); continue; }
                if ((now + pawn.thingIDNumber) % 60 == 0)
                {
                    UpdateTemperature(policy);
                    if (policy.autoUse)
                    {
                        var rescueDef = DefDatabase<ThingDef>.GetNamed("MX_RescuePill");
                        var rescueRule = policy.For(rescueDef);
                        if (rescueRule.mode != AutoMedicineMode.Off)
                        {
                            int count = pendant.Count(rescueDef);
                            if (count <= 1 && count != policy.lowRescueWarning)
                                Messages.Message("MX_RescueStock".Translate(pawn.LabelShort, count), pawn, MessageTypeDefOf.CautionInput);
                            policy.lowRescueWarning = count;
                            var rescue = pendant.Contents.FirstOrDefault(t => t.def == rescueDef);
                            if (rescue != null && MedicineCatalog.CanUse(rescue, pawn).Accepted && policy.pendingMedicine?.def != rescueDef) Start(pendant, rescue, false);
                        }
                        if (now >= policy.nextOrdinaryTick && !policy.pendingManual)
                        {
                            foreach (var item in pendant.Contents.OrderBy(t => Priority(t.def)).ThenBy(t => EffectRemaining(pawn, t.def)).ToList())
                                if ((policy.pendingMedicine == null || Priority(item.def) < Priority(policy.pendingMedicine.def)) &&
                                    Wants(policy, item) && Start(pendant, item, false).Accepted) break;
                        }
                    }
                }
                if (policy.pendingMedicine == null) continue;
                Thing dose = policy.pendingMedicine;
                if (policy.pendingPendant != pendant || !pendant.Contents.Contains(dose) ||
                    (!policy.pendingManual && (!policy.autoUse || !Wants(policy, dose)))) { policy.CancelDelivery(); continue; }
                if (now < policy.deliveryAt) continue;
                policy.CancelDelivery(); // Clear before effects so saving cannot replay a completed delivery.
                if (MedicineCatalog.Use(dose, pawn).Accepted) policy.nextOrdinaryTick = now + 300;
            }
        }
        private static void UpdateTemperature(PawnMedicinePolicy policy)
        {
            Pawn pawn = policy.pawn; int now = Find.TickManager.TicksGame;
            if (!pawn.Spawned) { policy.coldSince = policy.hotSince = -1; return; }
            bool warming = HasEffect(pawn, "MX_WarmingEffect"), cooling = HasEffect(pawn, "MX_CoolingEffect");
            float min = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin) + (warming ? 15 : 0);
            float max = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax) - (cooling ? 15 : 0);
            policy.coldSince = pawn.AmbientTemperature <= min + 2 ? (policy.coldSince < 0 ? now : policy.coldSince) : -1;
            policy.hotSince = pawn.AmbientTemperature >= max - 2 ? (policy.hotSince < 0 ? now : policy.hotSince) : -1;
        }
        private static bool HasEffect(Pawn pawn, string name) => ZombieMedicineService.Active(pawn, DefDatabase<HediffDef>.GetNamed(name));
        public static int EffectRemaining(Pawn pawn, ThingDef medicine)
        {
            if (medicine.defName == MedicineCatalog.Pearl) return pawn.health.hediffSet.GetFirstHediffOfDef(RMDefOf.RM_HediffYakshaPearl)?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 0;
            var effect = medicine.GetModExtension<MedicineExtension>()?.effect;
            return effect == null ? 0 : (pawn.health.hediffSet.GetFirstHediffOfDef(effect) as Hediff_TimedMedicine)?.RemainingTicks ?? 0;
        }
        private static int Priority(ThingDef medicine)
        {
            switch (medicine.defName)
            {
                case "MX_RescuePill": return 0;
                case "MX_PurifyPill": case "MX_CapacityPill": return 1;
                case "MX_RecoveryPill": return 2;
                case "MX_WarmingPill": case "MX_CoolingPill": return 3;
                case "MX_CombatPill": case "MX_DispelPill": return 4;
                case "MX_GatherPill": return 5;
                case "MX_IndustryPill": return 6;
                default: return 7;
            }
        }
        public static bool Combat(Pawn pawn) => pawn.Spawned && pawn.mindState?.enemyTarget is Thing target &&
            !target.Destroyed && target.HostileTo(pawn) && Find.TickManager.TicksGame - pawn.mindState.lastCombatantTick < 2500;
        private static bool QiAttack(Pawn pawn)
        {
            Verb verb = pawn.CurJob?.verbToUse;
            if (verb is Verb_LotlQiIntensifyShoot || verb is Verb_LotlQiSupplementShoot)
                return pawn.TryGetComp<CompAxolotlEnergy>()?.IsChangeLotiWeaponMode == true;
            if (verb?.IsMeleeAttack == true) return pawn.TryGetComp<CompAxolotlEnergy>()?.IsChangeLotiWeaponMode == true &&
                pawn.equipment?.Primary?.TryGetComp<Comp_LotiQiCloseCombatWeapon_ProjectileCost>() != null;
            return verb?.DirectOwner is Ability ability && ability.EffectComps.Any(c => c is CompAbilityAxolotlEnergyCost);
        }
        public static bool Wants(PawnMedicinePolicy policy, Thing item)
        {
            Pawn pawn = policy.pawn; var rule = policy.For(item.def);
            if (!Available(pawn) || rule.mode == AutoMedicineMode.Off || MedicineCatalog.ManualOnly(item.def) || !MedicineCatalog.CanUse(item, pawn).Accepted) return false;
            int now = Find.TickManager.TicksGame;
            if (item.def.defName == "MX_RescuePill") return true;
            if (pawn.Downed || pawn.InMentalState) return false;
            int remaining = EffectRemaining(pawn, item.def);
            if (remaining > 0 && (rule.mode != AutoMedicineMode.Maintain || !MedicineCatalog.Maintainable(item.def) || remaining > MedicineCatalog.Duration(item) + 60)) return false;
            switch (item.def.defName)
            {
                case "MX_WarmingPill": return policy.coldSince >= 0 && now - policy.coldSince >= 300;
                case "MX_CoolingPill": return policy.hotSince >= 0 && now - policy.hotSince >= 300;
                case "MX_IndustryPill": return pawn.Spawned && !pawn.Drafted && pawn.Awake() && pawn.CurJob?.workGiverDef != null;
                case "MX_RecoveryPill":
                    var qi = pawn.TryGetComp<CompAxolotlEnergy>();
                    return qi != null && qi.Energy <= qi.EnergyMax * 0.3f && MedicineService.RecoveryFor(pawn) > 0f;
                case "MX_CombatPill": return Combat(pawn);
                case "MX_DispelPill": return Combat(pawn) && QiAttack(pawn) &&
                    (pawn.CurJob?.targetA.Pawn ?? pawn.mindState.enemyTarget as Pawn)?.IsZombie(out _) == true;
                case "MX_GatherPill":
                    var malice = pawn.TryGetComp<CompYinAndMalevolent>();
                    return now >= policy.gatherPausedUntil && malice != null && malice.Malevolent < malice.MalevolentCapacity * 0.5f && ZombieMedicineService.GatherAmount(pawn) >= 200f;
                case MedicineCatalog.Pearl: return !policy.pearlPaused;
                case MedicineCatalog.Growth: return policy.growthTarget > 0 && (pawn.TryGetComp<CompAxolotlEnergy>()?.Level ?? 0) < policy.growthTarget;
                case "MX_PurifyPill": case "MX_CapacityPill": return true;
                default: return false;
            }
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref policies, "mxMedicinePolicies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) policies = policies?.Where(p => p.pawn != null).ToList() ?? new List<PawnMedicinePolicy>();
        }
    }
    [HarmonyPatch(typeof(TickList), nameof(TickList.Tick))]
    internal static class PendantBeforePawnTicks
    {
        private static void Prefix(TickerType ___tickType)
        { if (___tickType == TickerType.Normal) PendantManager.Instance?.ProcessTick(); }
    }
}
