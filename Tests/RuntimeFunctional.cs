// Real game-tick functional scenarios, compiled only into the isolated test mod.
using System;
using System.Collections.Generic;
using System.Linq;
using Axolotl;
using HarmonyLib;
using MoeLotlExpansion;
using RigorMortis;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoeLotlExpansionTests
{
    public sealed class FunctionalGame : GameComponent
    {
        private IEnumerator<int> steps;
        private int resumeAt;
        private bool finished;
        private Pawn actor, other, zombie;
        private Map map;
        public static Pawn ObservedAttacker, ObservedTarget;
        public static int Costs, DispelCalls;
        public static float ActualDispel;
        public FunctionalGame(Game game) { }
        private static ThingDef Def(string name) => DefDatabase<ThingDef>.GetNamed(name);
        private static Thing Make(string name, int count = 1)
        { var thing = ThingMaker.MakeThing(Def(name)); thing.stackCount = count; return thing; }
        private static Thing Pill(string name, int count = 1) => Make("MX_" + name + "Pill", count);
        private static void Check(bool ok, string label) => SmokeLog.Check(ok, "functional: " + label);
        private static void Clear(Pawn pawn, string name)
        { foreach (var h in pawn.health.hediffSet.hediffs.Where(h => h.def.defName == name).ToList()) pawn.health.RemoveHediff(h); }
        private static void Stop(Pawn pawn) { pawn.jobs.StopAll(); pawn.mindState.enemyTarget = null; pawn.mindState.lastCombatantTick = -99999; }
        private static MedicinePendant Wear(Pawn pawn)
        { var p = (MedicinePendant)Make("MX_MedicinePendant"); pawn.apparel.Wear(p); return p; }
        private static PawnMedicinePolicy Policy(Pawn pawn) => PendantManager.Instance.For(pawn);
        private IntVec3 Cell(int x, int z) => new IntVec3(map.Center.x + x, 0, map.Center.z + z);
        private Pawn Living(int x)
        {
            var pawn = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("AxolotlCombatant_Physics"), Faction.OfPlayer);
            GenSpawn.Spawn(pawn, Cell(x, 0), map);
            var qi = pawn.TryGetComp<CompAxolotlEnergy>();
            if (qi.PawnHediff == null) pawn.health.AddHediff(AxolotlHediffDefOf.Axolotl_LotlQi);
            qi.PawnHediff.Severity = 3; qi.Energy = qi.EnergyMax;
            pawn.skills.GetSkill(SkillDefOf.Melee).Level = 20; pawn.skills.GetSkill(SkillDefOf.Shooting).Level = 20;
            return pawn;
        }
        private void Move(Pawn pawn, IntVec3 cell) { Stop(pawn); pawn.Position = cell; }
        private static void Hold(Pawn pawn)
        { var job = JobMaker.MakeJob(JobDefOf.Wait); job.expiryInterval = 20000; pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc); }
        private void Equip(string name)
        {
            foreach (var weapon in actor.equipment.AllEquipmentListForReading.ToList()) { actor.equipment.Remove(weapon); weapon.Destroy(); }
            actor.equipment.AddEquipment((ThingWithComps)Make(name));
        }
        private static void Observe(Pawn attacker, Pawn target)
        { ObservedAttacker = attacker; ObservedTarget = target; Costs = DispelCalls = 0; ActualDispel = 0; }
        public override void GameComponentUpdate()
        {
            if (!GenCommandLine.CommandLineArgPassed("mx-functional") || finished || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            if (Find.TickManager.TicksGame < resumeAt) return;
            try
            {
                if (steps == null) { SmokeLog.Write("FUNCTIONAL_BEGIN"); steps = Run().GetEnumerator(); }
                if (!steps.MoveNext()) { finished = true; SmokeLog.Write("FUNCTIONAL_COMPLETE checks=" + SmokeLog.Checks); Application.Quit(); }
                else { resumeAt = Find.TickManager.TicksGame + steps.Current; Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast; }
            }
            catch (Exception e) { finished = true; SmokeLog.Write("FUNCTIONAL_FAIL " + e); Application.Quit(); }
        }
        private IEnumerable<int> Run()
        {
            map = Find.CurrentMap;
            // Clear only the generated test arena. Keep native defs and combat calculations intact.
            for (int x = -18; x <= 18; x++) for (int z = -12; z <= 12; z++)
            {
                IntVec3 c = Cell(x, z);
                foreach (var thing in c.GetThingList(map).ToList()) if (!(thing is Pawn)) thing.Destroy();
                map.terrainGrid.SetTerrain(c, DefDatabase<TerrainDef>.GetNamed("Soil"));
                map.roofGrid.SetRoof(c, null);
            }
            actor = Living(-8); other = Living(-5);
            zombie = RMUtility.TryCreateZombie(DefDatabase<MutantDef>.GetNamed("RM_GreenZombie"), faction: Faction.OfPlayer);
            AccessTools.Field(typeof(CompYinAndMalevolent), "dontHateSunlight").SetValue(zombie.TryGetComp<CompYinAndMalevolent>(), true);
            GenSpawn.Spawn(zombie, Cell(8, 0), map);
            var pendant = Wear(actor); var zp = Wear(zombie);
            var qi = actor.TryGetComp<CompAxolotlEnergy>();
            var policy = Policy(actor);
            pendant.Load(Pill("Recovery", 3), 3); qi.Energy = 0;
            yield return 240;
            Check(pendant.Count(Def("MX_RecoveryPill")) == 3 && policy.pendingMedicine == null, "default-off does not consume low-qi medicine");
            policy.autoUse = true; policy.For(Def("MX_RecoveryPill")).mode = AutoMedicineMode.Conditional;
            qi.Energy = 0;
            for (int i = 0; i < 65 && policy.pendingMedicine == null; i++) yield return 1;
            Check(policy.pendingMedicine != null, "auto recovery creates delayed delivery");
            policy.autoUse = false;
            yield return 120;
            Check(pendant.Count(Def("MX_RecoveryPill")) == 3 && policy.pendingMedicine == null, "switch off cancels without consumption");
            Check(PendantManager.Instance.Start(pendant, pendant.Contents.First(), true).Accepted, "manual works while auto off");
            actor.apparel.Remove(pendant);
            yield return 120;
            Check(pendant.Total == 3 && policy.pendingMedicine == null, "unequip cancels delivery with stock intact");
            other.apparel.Wear(pendant);
            Check(!Policy(other).autoUse && !Policy(other).autoRefill && Policy(other).entries.All(e => e.target == 0), "new owner inherits no permissions or procurement");
            other.apparel.Remove(pendant); actor.apparel.Wear(pendant);
            policy.autoUse = true; qi.Energy = 0;
            for (int i = 0; i < 65 && policy.pendingMedicine == null; i++) yield return 1;
            Check(PendantManager.Instance.Start(pendant, pendant.Contents.First(), true).Accepted, "manual replaces pending automatic request");
            yield return 130;
            Check(pendant.Count(Def("MX_RecoveryPill")) == 2, "competing manual and auto consume one");
            policy.autoUse = false;

            // Actual UseItem job, not a direct service invocation.
            Stop(actor); var warming = Pill("Warming", 2); GenSpawn.Spawn(warming, Cell(-7, 0), map);
            Check(actor.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("UseItem"), warming), JobTag.Misc), "UseItem order accepted");
            for (int i = 0; i < 120 && warming.stackCount == 2; i++) yield return 10;
            SmokeLog.Write("USE_STATE stack=" + warming.stackCount + " job=" + actor.CurJob + " usable=" + warming.TryGetComp<CompUsable>().CanBeUsedBy(actor));
            Check(warming.stackCount == 1 && ZombieMedicineService.Active(actor, DefDatabase<HediffDef>.GetNamed("MX_WarmingEffect")), "real UseItem job consumes one and applies warmth");

            // Actual feeding job and interrupted feeding.
            Move(actor, Cell(-2, 0)); Move(zombie, Cell(2, 0));
            Hold(zombie);
            var capacity = Pill("Capacity", 2); GenSpawn.Spawn(capacity, Cell(-1, 0), map);
            var feed = JobMaker.MakeJob(MXDefOf.MX_FeedMedicine, zombie, capacity); feed.count = 1;
            Check(actor.jobs.TryTakeOrderedJob(feed, JobTag.Misc), "feeding order accepted");
            for (int i = 0; i < 200 && !ZombieMedicineService.Active(zombie, MXDefOf.MX_CapacityEffect); i++) yield return 10;
            SmokeLog.Write("FEED_STATE job=" + actor.CurJob + " carried=" + actor.carryTracker.CarriedThing + " source=" + capacity.stackCount + " targetJob=" + zombie.CurJob);
            Check(ZombieMedicineService.Active(zombie, MXDefOf.MX_CapacityEffect), "real 120-tick feeding applies capacity");
            Check(capacity.stackCount == 1, "feeding takes exactly one from source stack");
            var purify = Pill("Purify", 2); GenSpawn.Spawn(purify, actor.Position, map);
            var interrupted = JobMaker.MakeJob(MXDefOf.MX_FeedMedicine, zombie, purify); interrupted.count = 1;
            actor.jobs.TryTakeOrderedJob(interrupted, JobTag.Misc);
            yield return 20; Stop(actor); yield return 150;
            Check(!ZombieMedicineService.Active(zombie, MXDefOf.MX_PurifyEffect), "interrupted feeding applies no effect");
            int purifyTotal = map.listerThings.ThingsOfDef(Def("MX_PurifyPill")).Sum(t => t.stackCount) + (actor.carryTracker.CarriedThing?.def == Def("MX_PurifyPill") ? actor.carryTracker.CarriedThing.stackCount : 0);
            Check(purifyTotal == 2, "interrupted feeding preserves both physical pills");

            // Target reduced while a real carrier is en route.
            Stop(actor); Stop(other); Move(zombie, Cell(15, 0)); Move(actor, Cell(-15, 0));
            Hold(zombie);
            var zpolicy = Policy(zombie); zpolicy.autoRefill = true;
            Check(zpolicy.SetTarget(zp, Def("MX_RescuePill"), 12), "restock target twelve accepted");
            var source = Pill("Rescue", 12); GenSpawn.Spawn(source, Cell(-14, 0), map);
            var refill = PendantRestock.FindJob(actor, zp); Check(refill != null, "refill work finds supply");
            actor.jobs.TryTakeOrderedJob(refill, JobTag.Misc);
            for (int i = 0; i < 100 && actor.carryTracker.CarriedThing?.def != Def("MX_RescuePill"); i++) yield return 1;
            Check(actor.carryTracker.CarriedThing?.def == Def("MX_RescuePill"), "carrier picked up physical rescue pills");
            Check(PendantRestock.FindJob(other, zp) == null, "second carrier cannot create duplicate request");
            Check(zpolicy.SetTarget(zp, Def("MX_RescuePill"), 2), "lower target during transfer");
            for (int i = 0; i < 300 && zp.Count(Def("MX_RescuePill")) < 2; i++) yield return 10;
            Check(zp.Count(Def("MX_RescuePill")) == 2, "in-flight refill obeys reduced target");
            zpolicy.autoRefill = false;
            int rescueTotal = map.listerThings.ThingsOfDef(Def("MX_RescuePill")).Sum(t => t.stackCount) + zp.Count(Def("MX_RescuePill")) +
                (actor.carryTracker.CarriedThing?.def == Def("MX_RescuePill") ? actor.carryTracker.CarriedThing.stackCount : 0);
            Check(rescueTotal == 12, "lowered target preserves leftover carried stock");
            Move(other, Cell(13, 0));
            GenSpawn.Spawn(Pill("Purify", 2), Cell(14, 0), map);
            var loadOne = PendantRestock.FindJob(other, zp, Def("MX_PurifyPill"), 1);
            Check(loadOne != null && zpolicy.For(Def("MX_PurifyPill")).target == 0, "manual single load allowed at zero target and refill off");
            other.jobs.TryTakeOrderedJob(loadOne, JobTag.Misc);
            for (int i = 0; i < 150 && zp.Count(Def("MX_PurifyPill")) == 0; i++) yield return 10;
            Check(zp.Count(Def("MX_PurifyPill")) == 1, "manual single load transfers exactly one");

            // Native jump container: keep a pending emergency dose through despawn.
            Stop(zombie);
            var berserk = DefDatabase<MentalStateDef>.AllDefsListForReading.First(d => d.stateClass == typeof(MentalState_ZombieBerserk));
            Check(zombie.mindState.mentalStateHandler.TryStartMentalState(berserk, forceWake: true), "jump rescue starts owned berserk");
            zombie.TryGetComp<CompYinAndMalevolent>().Malevolent = 1000;
            zpolicy.autoUse = true; zpolicy.For(Def("MX_RescuePill")).mode = AutoMedicineMode.Conditional;
            for (int i = 0; i < 65 && zpolicy.pendingMedicine == null; i++) yield return 1;
            Check(zpolicy.pendingMedicine?.def == Def("MX_RescuePill"), "automatic rescue schedules before jump");
            int jumpDeliveryAt = zpolicy.deliveryAt;
            var flyer = PawnFlyer.MakeFlyer(Def("Axolotl_MoveFlyer"), zombie, Cell(12, 0), null, null);
            GenSpawn.Spawn(flyer, Cell(15, 0), map);
            // Extend only this test flight so its airborne interval covers delivery.
            AccessTools.Field(typeof(PawnFlyer), "ticksFlightTime").SetValue(flyer, 240);
            yield return 10;
            Check(!zombie.Spawned && zombie.ParentHolder == flyer && zpolicy.deliveryAt == jumpDeliveryAt, "jump keeps original rescue deadline");
            yield return Math.Max(1, jumpDeliveryAt - Find.TickManager.TicksGame + 2);
            Check(!zombie.Spawned && !zombie.InMentalState && zp.Count(Def("MX_RescuePill")) == 1, "airborne rescue consumes one and restores control");
            SmokeLog.Near(SmokeLog.RescueAtConsumption, 0f, "airborne rescue clears malice at consumption");
            zpolicy.autoUse = false;
            yield return 350; // Also let the ordinary-dose 300-tick gap expire.
            Check(zombie.Spawned && !zombie.InMentalState, "native landing retains recovered control");
            Hold(zombie);
            Check(PendantManager.Instance.Start(zp, zp.Contents.First(t => t.def == Def("MX_PurifyPill")), true).Accepted, "prepare nonflight removal cancellation");
            zombie.DeSpawn();
            yield return 1;
            Check(!PendantManager.Available(zombie) && zpolicy.pendingMedicine == null && zp.Count(Def("MX_PurifyPill")) == 1, "nonflight removal still cancels without consuming");
            GenSpawn.Spawn(zombie, Cell(12, 0), map);
            zombie.TryGetComp<CompYinAndMalevolent>().Malevolent = 500;

            // Native melee verb: natural hit/miss, real qi expenditure and native damage.
            Stop(actor); Stop(zombie); actor.drafter.Drafted = true;
            Move(actor, Cell(-2, 4)); Move(zombie, Cell(-1, 4));
            Hold(zombie);
            Equip("Axolotl_WindWrappingSword"); qi.IsChangeLotiWeaponMode = true; qi.Energy = qi.EnergyMax;
            Check(MedicineService.TryUse(Pill("Dispel"), actor).Accepted, "dispel enabled for native attacks");
            Observe(actor, zombie);
            for (int i = 0; i < 12 && Costs == 0; i++)
            {
                Move(zombie, Cell(-1, 4));
                Hold(zombie);
                actor.meleeVerbs.TryMeleeAttack(zombie, actor.equipment.Primary.TryGetComp<CompEquippable>().AllVerbs.First(v => v.IsMeleeAttack));
                yield return 200;
            }
            SmokeLog.Write("MELEE costs=" + Costs + " dispels=" + DispelCalls + " amount=" + ActualDispel);
            Check(Costs > 0 && DispelCalls > 0 && ActualDispel > 0, "native qi sword hit dispels without injected attack scope");

            // Real four-shot volley and actual travelling projectiles.
            Move(actor, Cell(-4, 5)); Move(zombie, Cell(4, 5));
            Hold(zombie);
            foreach (var h in zombie.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()) zombie.health.RemoveHediff(h);
            Equip("Axolotl_MoeLotlCrossBow_Intermediate"); qi.Energy = qi.EnergyMax; qi.IsChangeLotiWeaponMode = true;
            yield return 240;
            Observe(actor, zombie);
            var ranged = actor.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb;
            Check(ranged.TryStartCastOn(zombie, preventFriendlyFire: false), "native crossbow starts volley");
            yield return 240;
            SmokeLog.Write("RANGED costs=" + Costs + " dispels=" + DispelCalls + " amount=" + ActualDispel);
            Check(Costs > 0, "native volley spends qi");
            Check(DispelCalls == 1, "multi-projectile volley dispels same target once");
            foreach (var h in zombie.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()) zombie.health.RemoveHediff(h);
            Observe(actor, zombie); qi.IsChangeLotiWeaponMode = false;
            Check(ranged.TryStartCastOn(zombie, preventFriendlyFire: false), "ordinary volley starts"); yield return 240;
            Check(Costs == 0 && DispelCalls == 0, "ordinary volley never dispels");
            actor.drafter.Drafted = false; Stop(actor); Stop(zombie);

            // Physical container destruction and actual caravan membership.
            var dropped = (MedicinePendant)Make("MX_MedicinePendant"); var cooling = Pill("Cooling", 7); dropped.Load(cooling, 7);
            GenSpawn.Spawn(dropped, Cell(0, 8), map); int before = map.listerThings.ThingsOfDef(Def("MX_CoolingPill")).Sum(t => t.stackCount);
            dropped.Destroy(); Check(map.listerThings.ThingsOfDef(Def("MX_CoolingPill")).Sum(t => t.stackCount) == before + 7, "destroyed pendant releases stock exactly once");
            Clear(actor, "MX_RecoveryEffect"); policy.nextOrdinaryTick = 0; qi.Energy = 0;
            actor.DeSpawn(); var caravan = CaravanMaker.MakeCaravan(new[] { actor }, Faction.OfPlayer, map.Tile, true);
            Check(actor.IsCaravanMember(), "actor is in actual world caravan");
            int carried = pendant.Count(Def("MX_RecoveryPill")); policy.autoUse = true;
            yield return 200;
            Check(pendant.Count(Def("MX_RecoveryPill")) == carried - 1, "automatic recovery works during real caravan ticks");
            var grow = Make(MedicineCatalog.Growth); pendant.Load(grow, 1);
            Check(!MedicineCatalog.CanUse(grow, actor).Accepted, "native map-effect pill pauses in caravan");
            policy.autoUse = false;
            SmokeLog.Write("FUNCTIONAL_SCENARIOS_FINISHED");
        }
    }
    [HarmonyPatch(typeof(AxolotlUtility.MoelotlEnergyUtility), nameof(AxolotlUtility.MoelotlEnergyUtility.DoEnergyCost))]
    internal static class ObserveFunctionalCost
    {
        private static void Postfix(Pawn pawn, float costAmount, bool __result)
        { if (pawn == FunctionalGame.ObservedAttacker && __result && costAmount > 0) FunctionalGame.Costs++; }
    }
    [HarmonyPatch(typeof(RMUtility), nameof(RMUtility.ChangeMalevolent), new[] { typeof(CompYinAndMalevolent), typeof(float), typeof(bool) })]
    internal static class ObserveFunctionalDispel
    {
        private static void Prefix(CompYinAndMalevolent comp, out float __state) => __state = comp.Malevolent;
        private static void Postfix(CompYinAndMalevolent comp, float num, float __state)
        {
            if (comp.Zombie == FunctionalGame.ObservedTarget && num == -20f)
            { FunctionalGame.DispelCalls++; FunctionalGame.ActualDispel += __state - comp.Malevolent; }
        }
    }
}
