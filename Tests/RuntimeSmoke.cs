// Compiled separately into an isolated test mod; never shipped in the expansion.
using System;
using System.IO;
using System.Linq;
using Axolotl;
using HarmonyLib;
using MoeLotlExpansion;
using RigorMortis;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoeLotlExpansionTests
{
    [StaticConstructorOnStartup]
    public static class SmokeLog
    {
        public static readonly bool Enabled = GenCommandLine.CommandLineArgPassed("mx-smoke");
        public static int Checks;
        public static float RescueAtConsumption = -1;
        static SmokeLog()
        {
            if (Enabled || GenCommandLine.CommandLineArgPassed("mx-functional")) new Harmony("pigeonhunter715.moelotl.expansion.tests").PatchAll(typeof(SmokeLog).Assembly);
            if (Enabled) LongEventHandler.ExecuteWhenFinished(() =>
            {
                Write("DEFS_LOADED");
                Write("PATCHED_METHODS " + Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m).Owners.Contains("pigeonhunter715.moelotl.expansion")));
            });
        }
        public static void Write(string text)
        { Log.Message("MX_SMOKE " + text); File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "mx-smoke.txt"), text + Environment.NewLine); }
        public static void Check(bool okay, string name)
        { if (!okay) throw new Exception(name); Checks++; Write("PASS " + name); }
        public static void Near(float actual, float expected, string name) => Check(Math.Abs(actual - expected) < 0.02f, name + " actual=" + actual + " expected=" + expected);
    }

    public sealed class SmokeGame : GameComponent
    {
        private int stage, due, savedCount, savedRemaining;
        private int renewalAt;
        private long renewalExpiry;
        private Pawn living, zombie, taiyin;
        private MedicinePendant pendant;
        private bool busy;
        private float captureAt;
        public SmokeGame(Game game) { }
        private static ThingDef Def(string name) => DefDatabase<ThingDef>.GetNamed(name);
        private static Thing Pill(string key, int count = 2)
        { var thing = ThingMaker.MakeThing(Def("MX_" + key + "Pill")); thing.stackCount = count; return thing; }
        private static void Dose(string key, Pawn pawn)
        { var pill = Pill(key); var result = MedicineService.TryUse(pill, pawn); SmokeLog.Check(result.Accepted, key + " accepted: " + result.Reason); SmokeLog.Check(pill.stackCount == 1 && !pill.Destroyed, key + " consumes exactly one"); }
        public override void GameComponentUpdate()
        {
            if (!SmokeLog.Enabled || busy || stage >= 9 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            busy = true;
            try
            {
                if (stage == 0) RunInitial();
                else if (stage == 1)
                {
                    stage = 9; // Reload restores the saved stage=2.
                    GameDataSaveLoader.LoadGame("MX-isolated-smoke");
                }
                else if (stage == 2) VerifyReload();
                else if (stage == 3 && Find.TickManager.TicksGame >= due) VerifyDelivery();
                else if (stage == 4 && Find.TickManager.TicksGame >= due) VerifyRescue();
                else if (stage == 5 && Find.TickManager.TicksGame >= renewalAt) VerifyRenewal();
                else if (stage == 6) VerifyRestock();
                else if (stage == 7 && Time.realtimeSinceStartup >= captureAt)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "pendant-ui.png"));
                    stage = 8; captureAt = Time.realtimeSinceStartup + 2;
                }
                else if (stage == 8 && Time.realtimeSinceStartup >= captureAt)
                { stage = 9; SmokeLog.Write("COMPLETE checks=" + SmokeLog.Checks); Application.Quit(); }
            }
            catch (Exception error) { stage = 9; SmokeLog.Write("FAIL " + error); Application.Quit(); }
            finally { busy = false; }
        }
        private void RunInitial()
        {
            SmokeLog.Write("MAP_READY");
            Map map = Find.CurrentMap;
            living = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("AxolotlCombatant_Physics"), Faction.OfPlayer);
            GenSpawn.Spawn(living, CellFinder.RandomClosewalkCellNear(map.Center, map, 8), map);
            SmokeLog.Check(living.IsMoeLotl() && !living.IsZombie(out _), "living pawn generated");
            var qi = living.TryGetComp<CompAxolotlEnergy>();
            if (qi.PawnHediff == null) living.health.AddHediff(AxolotlHediffDefOf.Axolotl_LotlQi);
            qi.PawnHediff.Severity = 3;
            float cold = living.GetStatValue(StatDefOf.ComfyTemperatureMin, true, 0);
            float hot = living.GetStatValue(StatDefOf.ComfyTemperatureMax, true, 0);
            Dose("Warming", living); Dose("Cooling", living); Dose("Industry", living);
            SmokeLog.Near(living.GetStatValue(StatDefOf.ComfyTemperatureMin, true, 0), cold - 15, "warming stat");
            SmokeLog.Near(living.GetStatValue(StatDefOf.ComfyTemperatureMax, true, 0), hot + 15, "cooling stat");
            qi.Energy = 0; float gain = MedicineService.RecoveryFor(living);
            Dose("Recovery", living); SmokeLog.Near(qi.Energy, gain, "real energy recovery");
            SmokeLog.Check(!MedicineService.CanUse(Pill("Recovery"), living).Accepted, "recovery cooldown blocks repeat");
            Dose("Combat", living); Dose("Dispel", living);
            SmokeLog.Check(!MedicineService.CanUse(Pill("Combat"), living).Accepted, "combat repeat blocked");
            zombie = RMUtility.TryCreateZombie(DefDatabase<MutantDef>.GetNamed("RM_GreenZombie"), faction: Faction.OfPlayer);
            AccessTools.Field(typeof(CompYinAndMalevolent), "dontHateSunlight").SetValue(zombie.TryGetComp<CompYinAndMalevolent>(), true);
            GenSpawn.Spawn(zombie, CellFinder.RandomClosewalkCellNear(map.Center, map, 8), map);
            SmokeLog.Check(zombie.IsZombie(out _), "zombie generated");
            var malice = zombie.TryGetComp<CompYinAndMalevolent>();
            float capacity = malice.MalevolentCapacity;
            Dose("Capacity", zombie); SmokeLog.Near(malice.MalevolentCapacity, capacity * 1.5f, "capacity is dynamic");
            Dose("Purify", zombie); malice.Malevolent = 99999;
            SmokeLog.Near(malice.Malevolent, malice.MalevolentCapacity * 0.8f, "purify clamps writes to eighty percent");
            zombie.health.AddHediff(RMDefOf.RM_HediffYakshaPearl);
            float before = malice.Malevolent; Dose("Release", zombie);
            SmokeLog.Near(malice.Malevolent, before, "release preserves current malice");
            SmokeLog.Check(!zombie.health.hediffSet.HasHediff(RMDefOf.RM_HediffYakshaPearl) && !ZombieMedicineService.Active(zombie, MXDefOf.MX_PurifyEffect), "release removes both bindings");
            malice.Malevolent = 0; float dose = ZombieMedicineService.GatherAmount(zombie);
            Dose("Gather", zombie); SmokeLog.Near(malice.Malevolent, dose, "gather uses actual gain");
            malice.Malevolent = 500;
            ProbeAttack(false); SmokeLog.Near(malice.Malevolent, 500, "unpaid hit does not dispel");
            ProbeAttack(true); SmokeLog.Near(malice.Malevolent, 480, "paid direct hit dispels twenty once");
            ProbeAttack(true); SmokeLog.Near(malice.Malevolent, 480, "pair cooldown blocks repeated paid hit");
            foreach (string furnaceName in new[] { "Axolotl_AlchemyStove_Neolithic", "Axolotl_AlchemyStove_Industrial" })
            {
                var furnace = (Building_AlchemyStove_Base)ThingMaker.MakeThing(Def(furnaceName));
                GenSpawn.Spawn(furnace, CellFinder.RandomClosewalkCellNear(map.Center, map, 15), map);
                foreach (var batch in new[] { (Key: "Recovery", Yield: 50), (Key: "Dispel", Yield: 25), (Key: "Combat", Yield: 25), (Key: "Capacity", Yield: 3), (Key: "Purify", Yield: 3) })
                {
                    var pill = Def("MX_" + batch.Key + "Pill");
                    string context = furnaceName + " " + batch.Key;
                    furnace.Action_ResetTargetPill(pill);
                    SmokeLog.Check(furnace.CurrentTargetPill_StuffCountMax == 5 && furnace.CurrentTargetPill_FinishCount == batch.Yield, context + " batch input and yield");
                    Thing preparation = ThingMaker.MakeThing(Def("MX_" + batch.Key + "Preparation"));
                    preparation.stackCount = 6;
                    furnace.Action_AddRefineStuff(preparation);
                    SmokeLog.Check(preparation.stackCount == 1 && furnace.CurrentTargetPill_StuffCount == 5, context + " consumes five preparations");
                    int prior = map.listerThings.ThingsOfDef(pill).Sum(t => t.stackCount);
                    furnace.Action_FinishRefine();
                    SmokeLog.Check(map.listerThings.ThingsOfDef(pill).Sum(t => t.stackCount) == prior + batch.Yield,
                        context + " produces " + batch.Yield + " physical pills");
                    preparation.Destroy();
                }
                furnace.Destroy();
            }
            SmokeLog.Check(!MedicineService.CanUse(Pill("Warming"), zombie).Accepted, "living medicine rejects zombie");
            VerifyTaiyin(map);
            pendant = (MedicinePendant)ThingMaker.MakeThing(Def("MX_MedicinePendant"));
            VerifyPendantQualityAndReduction(pendant);
            float emptyValue = pendant.MarketValue;
            var policy = PendantManager.Instance.For(living);
            SmokeLog.Check(!policy.autoUse && !policy.autoRefill && policy.entries.All(e => e.target == 0 && e.mode == AutoMedicineMode.Off), "all automation defaults off");
            var warming = Pill("Warming", 25);
            SmokeLog.Check(pendant.Load(warming, 25) == 25, "load physical stack");
            SmokeLog.Check(pendant.Load(Pill("Warming"), 1) == 0, "per type limit");
            SmokeLog.Near(pendant.GetStatValue(StatDefOf.Mass), 1.55f, "contents contribute mass");
            SmokeLog.Near(pendant.MarketValue, emptyValue + 25 * Def("MX_WarmingPill").BaseMarketValue, "contents contribute market value once");
            foreach (string key in new[] { "Cooling", "Recovery", "Combat", "Dispel", "Industry" }) SmokeLog.Check(policy.SetTarget(pendant, Def("MX_" + key + "Pill"), 1), "reserve slot " + key);
            SmokeLog.Check(pendant.Load(Pill("Gather"), 1) == 0, "reserved slots reject seventh medicine");
            SmokeLog.Check(policy.SetTarget(pendant, Def("MX_WarmingPill"), 10), "target accepted");
            var item = pendant.Contents.First();
            SmokeLog.Check(PendantManager.Instance.Start(pendant, item, true).Accepted, "manual delivery starts with auto off");
            savedCount = pendant.Total; savedRemaining = ((Hediff_TimedMedicine)living.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("MX_WarmingEffect"))).RemainingTicks;
            // Awful is the only vanilla quality without a <quality> node; the explicit
            // marker must preserve it instead of treating the pendant as legacy.
            pendant.GetComp<CompQuality>().SetQuality(QualityCategory.Awful, null);
            stage = 2; GameDataSaveLoader.SaveGame("MX-isolated-smoke");
            SmokeLog.Check(File.Exists(GenFilePaths.FilePathForSavedGame("MX-isolated-smoke")), "isolated save written");
            stage = 1;
        }
        private void VerifyTaiyin(Map map)
        {
            var medicineBills = new[] { "MX_MakeIndustryPillsx5", "MX_MakeRecoveryPreparation", "MX_MakeTaiyinPreparation" };
            foreach (string name in medicineBills)
            {
                var recipe = DefDatabase<RecipeDef>.GetNamed(name);
                var bill = new Bill_Production(recipe, null);
                SmokeLog.Check(bill.LabelCap.EndsWith(" ×" + recipe.products.Single().count, StringComparison.Ordinal),
                    name + " shows the quantity on the in-game bill");
            }
            var research = DefDatabase<ResearchProjectDef>.GetNamed("Axolotl_Research_HeavenlyTopArmor");
            SmokeLog.Check(research.UnlockedDefs.Contains(Def("MX_TaiyinPill")), "late research shows finished Taiyin pill");
            research = DefDatabase<ResearchProjectDef>.GetNamed("Axolotl_Research_Alchemy");
            SmokeLog.Check(research.UnlockedDefs.Contains(Def("MX_RecoveryPill")), "alchemy research shows finished recovery pill");

            var human = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            foreach (string key in new[] { "Capacity", "Purify", "Gather", "Release", "Rescue" })
            {
                Thing pill = Pill(key);
                SmokeLog.Check(!MedicineService.TryUse(pill, living).Accepted && !MedicineService.TryUse(pill, human).Accepted &&
                    pill.stackCount == 2, key + " rejects living MoeLotl and human without consuming a pill");
                pill.Destroy();
            }

            var flying = DefDatabase<MutantDef>.GetNamed("RM_FlyingZombie");
            var unqualified = RMUtility.TryCreateZombie(flying, faction: Faction.OfPlayer);
            GenSpawn.Spawn(unqualified, CellFinder.RandomClosewalkCellNear(map.Center, map, 8), map);
            var unqualifiedMalice = unqualified.TryGetComp<CompYinAndMalevolent>();
            unqualifiedMalice.Awake();
            unqualifiedMalice.Malevolent = 2500f;
            Thing lethal = Pill("Taiyin", 1);
            MedicineService.TryUse(lethal, unqualified);
            SmokeLog.Check(unqualified.Dead && lethal.Destroyed, "unqualified immortal flying zombie dies and consumes Taiyin");

            taiyin = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("AxolotlCombatant_Physics"), Faction.OfPlayer);
            taiyin.health.GetOrAddHediff(AxolotlHediffDefOf.Axolotl_LotlQi).Severity = 5f;
            taiyin.CorpseTransform(flying, awake: true);
            GenSpawn.Spawn(taiyin, CellFinder.RandomClosewalkCellNear(map.Center, map, 8), map);
            var comp = taiyin.TryGetComp<CompYinAndMalevolent>();
            if (!taiyin.story.traits.HasTrait(TraitDefOf.Kind)) taiyin.story.traits.GainTrait(new Trait(TraitDefOf.Kind));
            var record = DefDatabase<RecordDef>.GetNamed("MX_TaiyinZombieHumanlikeKills");
            RecordsUtility.Notify_PawnKilled(human, living);
            SmokeLog.Check(living.records.GetValue(record) == 0f, "living kills do not count toward Taiyin");
            for (int i = 0; i < 999; i++) taiyin.records.Increment(record);
            RecordsUtility.Notify_PawnKilled(human, taiyin);
            SmokeLog.Check(taiyin.records.GetValue(record) == 1000f, "native kill notification awards qualifying zombie kill");
            float excess = comp.MalevolentCapacity + 1000f;
            comp.Malevolent = excess;
            SmokeLog.Check(comp.Danger, "flying zombie has overcapacity danger before Taiyin");
            Thing dose = Pill("Taiyin", 1);
            TaiyinEligibility eligibility = TaiyinMedicine.Evaluate(taiyin);
            SmokeLog.Check(eligibility.Eligible, "flying zombie meets Taiyin conditions: " +
                string.Join(", ", TaiyinMedicine.ReasonLines(eligibility)));
            AcceptanceReport taken = MedicineService.TryUse(dose, taiyin);
            SmokeLog.Check(taken.Accepted, "qualified Taiyin dose accepted: " + taken.Reason);
            Hediff taiyinEffect = taiyin.health.hediffSet.GetFirstHediffOfDef(TaiyinMedicine.EffectDef);
            SmokeLog.Check(!taiyin.Dead && dose.Destroyed && taiyinEffect != null,
                "qualified Taiyin dose transforms and is consumed");
            SmokeLog.Check(taiyinEffect.LabelColor == new Color(1f, 0.84f, 0.30f), "Taiyin health label uses gold");
            SmokeLog.Check(!comp.Danger && comp.Malevolent == excess, "Taiyin prevents overcapacity berserk and preserves actual malice");
            SmokeLog.Check(taiyin.story.traits.HasTrait(DefDatabase<TraitDef>.GetNamed("Beauty"), 2) &&
                taiyin.story.traits.allTraits.Any(trait => trait.def.defName == "PerfectMemory" || trait.def.defName == "MX_PerfectMemory"),
                "Taiyin grants beautiful and perfect memory traits");

            var bodyRepair = taiyin.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("RM_Axolotl_BodyRepair"));
            var repair = bodyRepair.TryGetComp<CompZombieBodyRepair>();
            var injury = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"), taiyin, taiyin.RaceProps.body.corePart);
            injury.Severity = 10f;
            taiyin.health.AddHediff(injury);
            repair.RePair(taiyin, out bool healed);
            float amount = 3f * repair.Props.repairAmountPerDay * bodyRepair.Severity * repair.Props.repairTick / GenDate.TicksPerDay;
            SmokeLog.Check(healed, "native body repair reports successful healing");
            SmokeLog.Near(injury.Severity, 10f - amount, "Taiyin triples native wound repair");
        }
        private void VerifyPendantQualityAndReduction(MedicinePendant target)
        {
            var qi = living.TryGetComp<CompAxolotlEnergy>();
            int baselineShield = qi.ShieldMax;
            var baselineCut = new DamageInfo(DamageDefOf.Cut, 100f, armorPenetration: 999f);
            living.PreApplyDamage(ref baselineCut, out bool baselineAbsorbed);
            float baselineAmount = baselineCut.Amount;
            SmokeLog.Check(!baselineAbsorbed && baselineAmount > 0f, "baseline melee cut is external");
            var quality = target.GetComp<CompQuality>();
            SmokeLog.Check(quality != null, "pendant has quality comp");
            quality.SetQuality(QualityCategory.Normal, null);
            living.apparel.Wear(target);
            SmokeLog.Check(qi.ShieldMax == baselineShield + 16, "normal pendant adds sixteen native shield");
            var normalCut = new DamageInfo(DamageDefOf.Cut, 100f, armorPenetration: 999f);
            living.PreApplyDamage(ref normalCut, out bool normalAbsorbed);
            SmokeLog.Check(!normalAbsorbed, "normal pendant leaves melee cut unabsorbed");
            SmokeLog.Near(normalCut.Amount, baselineAmount * 0.93f, "normal pendant reduces leftover cut by seven percent");
            quality.SetQuality(QualityCategory.Legendary, null);
            SmokeLog.Check(qi.ShieldMax == baselineShield + 32, "legendary pendant adds thirty-two native shield");
            var legendaryCut = new DamageInfo(DamageDefOf.Cut, 100f, armorPenetration: 999f);
            living.PreApplyDamage(ref legendaryCut, out bool legendaryAbsorbed);
            SmokeLog.Check(!legendaryAbsorbed, "legendary pendant leaves melee cut unabsorbed");
            SmokeLog.Near(legendaryCut.Amount, baselineAmount * 0.85f, "legendary pendant reduces leftover cut by fifteen percent");
            qi.Shield = qi.ShieldMax;
            float chargedShield = qi.Shield;
            var bullet = new DamageInfo(DamageDefOf.Bullet, 20f);
            living.PreApplyDamage(ref bullet, out bool bulletAbsorbed);
            SmokeLog.Check(bulletAbsorbed && qi.Shield < chargedShield, "native qi shield absorbs a bullet and spends its charge");
            qi.Shield = 0f;
            quality.SetQuality(QualityCategory.Normal, null);
            SmokeLog.Check(qi.ShieldMax == baselineShield + 16, "normal quality restores sixteen native shield");
        }
        private void ProbeAttack(bool paid)
        {
            // Inject an attack scope, then exercise actual patched cost/damage APIs.
            // This is an integration probe, not a native weapon animation test.
            var scopeType = typeof(DispelTracker).Assembly.GetType("MoeLotlExpansion.AttackScope");
            object scope = Activator.CreateInstance(scopeType, DispelTracker.Instance.Begin(living));
            if (paid) AxolotlUtility.MoelotlEnergyUtility.DoEnergyCost(living, 1);
            zombie.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 0f, instigator: living));
            zombie.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 0f, instigator: living));
            scopeType.GetMethod("Finish").Invoke(scope, new object[] { true });
        }
        private void VerifyReload()
        {
            SmokeLog.Check(living != null && zombie != null && pendant != null && MedicinePendant.Active(living) == pendant, "pawn and apparel references restored");
            SmokeLog.Check(pendant.Total == savedCount, "physical contents survive reload");
            var reloadedQuality = pendant.GetComp<CompQuality>();
            SmokeLog.Check(reloadedQuality != null && reloadedQuality.Quality == QualityCategory.Awful, "explicit awful quality survives reload");
            SmokeLog.Check(taiyin != null && taiyin.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("MX_TaiyinEffect")) &&
                taiyin.records.GetValue(DefDatabase<RecordDef>.GetNamed("MX_TaiyinZombieHumanlikeKills")) == 1000f,
                "Taiyin transformation and zombie kill record survive reload");
            var policy = PendantManager.Instance.For(living);
            SmokeLog.Check(!policy.autoUse && !policy.autoRefill && policy.For(Def("MX_WarmingPill")).target == 10, "policy persists independently");
            SmokeLog.Check(policy.pendingMedicine != null && pendant.Contents.Contains(policy.pendingMedicine) && policy.pendingManual, "pending delivery reference restored");
            int remaining = ((Hediff_TimedMedicine)living.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("MX_WarmingEffect"))).RemainingTicks;
            SmokeLog.Check(Math.Abs(remaining - savedRemaining) < 10, "effect clock survives reload");
            due = policy.deliveryAt + 5; stage = 3; Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        }
        private void VerifyDelivery()
        {
            SmokeLog.Check(pendant.Total == savedCount - 1 && PendantManager.Instance.For(living).pendingMedicine == null, "saved delivery consumes one and completes");
            var zombiePendant = (MedicinePendant)ThingMaker.MakeThing(Def("MX_MedicinePendant"));
            zombie.apparel.Wear(zombiePendant);
            zombiePendant.Load(Pill("Rescue"), 2);
            var policy = PendantManager.Instance.For(zombie);
            SmokeLog.Check(!policy.autoUse && !policy.autoRefill, "zombie independent defaults off");
            // Keep the test away from quickstart colonists and random friendly fire.
            zombie.jobs.StopAll();
            zombie.Position = CellFinder.RandomClosewalkCellNear(new IntVec3(15, 0, 15), zombie.Map, 5);
            living.Position = CellFinder.RandomClosewalkCellNear(zombie.Map.Center, zombie.Map, 5);
            var state = DefDatabase<MentalStateDef>.AllDefsListForReading.First(d => d.stateClass == typeof(MentalState_ZombieBerserk));
            SmokeLog.Check(zombie.mindState.mentalStateHandler.TryStartMentalState(state, forceWake: true), "start owned zombie berserk");
            zombie.TryGetComp<CompYinAndMalevolent>().Malevolent = 1000;
            policy.autoUse = true; policy.For(Def("MX_RescuePill")).mode = AutoMedicineMode.Conditional;
            due = Find.TickManager.TicksGame + 185; stage = 4;
        }
        private void VerifyRescue()
        {
            SmokeLog.Write("RESCUE_STATE tick=" + Find.TickManager.TicksGame + " spawned=" + zombie.Spawned + " dead=" + zombie.Dead + " downed=" + zombie.Downed +
                " faction=" + zombie.Faction + " state=" + zombie.MentalStateDef + " stock=" + MedicinePendant.Active(zombie)?.Count(Def("MX_RescuePill")) +
                " pending=" + PendantManager.Instance.For(zombie).pendingMedicine + " due=" + PendantManager.Instance.For(zombie).deliveryAt + " consumedMalice=" + SmokeLog.RescueAtConsumption);
            SmokeLog.Check(!zombie.InMentalState && zombie.Faction == Faction.OfPlayer, "automatic rescue restores control");
            SmokeLog.Check(MedicinePendant.Active(zombie).Count(Def("MX_RescuePill")) == 1, "automatic rescue consumes exactly one");
            SmokeLog.Near(SmokeLog.RescueAtConsumption, 0f, "rescue clears malice at consumption before natural growth");
            var policy = PendantManager.Instance.For(zombie);
            SmokeLog.Check(policy.pearlPaused && policy.gatherPausedUntil > Find.TickManager.TicksGame, "rescue pauses pearl and gather");
            SmokeLog.Write("ZOMBIE_HEALTH " + string.Join(",", zombie.health.hediffSet.hediffs.Select(h => h.def.defName + ":" + h.Severity)));
            foreach (Hediff injury in zombie.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()) zombie.health.RemoveHediff(injury);
            var effect = (Hediff_TimedMedicine)zombie.health.hediffSet.GetFirstHediffOfDef(MXDefOf.MX_CapacityEffect);
            effect.Renew(600); renewalExpiry = effect.ExpiresAtTick; renewalAt = (int)renewalExpiry + 3;
            MedicinePendant.Active(zombie).Load(Pill("Capacity"), 2);
            policy.For(Def("MX_CapacityPill")).mode = AutoMedicineMode.Maintain;
            stage = 5;
        }
        private void VerifyRenewal()
        {
            var effect = zombie.health.hediffSet.GetFirstHediffOfDef(MXDefOf.MX_CapacityEffect) as Hediff_TimedMedicine;
            SmokeLog.Write("RENEWAL_STATE now=" + Find.TickManager.TicksGame + " downed=" + zombie.Downed + " expiry=" + effect?.ExpiresAtTick +
                " expected=" + (renewalExpiry + 600000) + " pending=" + PendantManager.Instance.For(zombie).pendingMedicine + " due=" + PendantManager.Instance.For(zombie).deliveryAt);
            SmokeLog.Check(effect != null && effect.ExpiresAtTick == renewalExpiry + 600000, "maintain renews exactly at expiry without early extension");
            SmokeLog.Check(MedicinePendant.Active(zombie).Count(Def("MX_CapacityPill")) == 1, "renewal consumes one");
            var policy = PendantManager.Instance.For(living);
            policy.SetTarget(pendant, Def("MX_WarmingPill"), 25); policy.manualRefill = true;
            living.mindState.enemyTarget = null; living.jobs.StopAll();
            Thing supply = Pill("Warming", 3); GenSpawn.Spawn(supply, CellFinder.RandomClosewalkCellNear(living.Position, living.Map, 2), living.Map);
            var job = PendantRestock.FindJob(living, pendant);
            SmokeLog.Check(job != null && job.count == 1, "real self refill job requests exact deficit");
            living.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            due = Find.TickManager.TicksGame + 2500; stage = 6; Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
        private void VerifyRestock()
        {
            if (pendant.Count(Def("MX_WarmingPill")) < 25 && Find.TickManager.TicksGame < due) return;
            SmokeLog.Check(pendant.Count(Def("MX_WarmingPill")) == 25, "real hauling job reaches target without duplication");
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Find.WindowStack.Add(new Dialog_MedicinePendant(pendant));
            captureAt = Time.realtimeSinceStartup + 3; stage = 7;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref stage, "stage"); Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref savedCount, "savedCount"); Scribe_Values.Look(ref savedRemaining, "savedRemaining");
            Scribe_References.Look(ref living, "living"); Scribe_References.Look(ref zombie, "zombie"); Scribe_References.Look(ref pendant, "pendant");
            Scribe_References.Look(ref taiyin, "taiyin");
        }
    }
    [HarmonyPatch(typeof(ZombieMedicineService), nameof(ZombieMedicineService.Apply))]
    internal static class ObserveRescue
    {
        private static void Postfix(Pawn pawn, MedicineExtension config, AcceptanceReport __result)
        {
            if (__result.Accepted && config.zombieMedicine == ZombieMedicine.Rescue)
                SmokeLog.RescueAtConsumption = pawn.TryGetComp<CompYinAndMalevolent>().Malevolent;
        }
    }
    [HarmonyPatch(typeof(PendantManager), nameof(PendantManager.Start))]
    internal static class ObserveRescueStart
    {
        private static void Postfix(MedicinePendant pendant, Thing item, AcceptanceReport __result)
        {
            if (SmokeLog.Enabled && item.def.defName == "MX_RescuePill")
                SmokeLog.Write("RESCUE_START tick=" + Find.TickManager.TicksGame + " accepted=" + __result.Accepted + " due=" + PendantManager.Instance.For(pendant.Wearer).deliveryAt);
        }
    }
    [HarmonyPatch(typeof(PawnMedicinePolicy), nameof(PawnMedicinePolicy.CancelDelivery))]
    internal static class ObserveRescueCancel
    {
        private static void Prefix(PawnMedicinePolicy __instance)
        {
            if (SmokeLog.Enabled && __instance.pendingMedicine?.def.defName == "MX_RescuePill")
                SmokeLog.Write("RESCUE_CLEAR tick=" + Find.TickManager.TicksGame + " due=" + __instance.deliveryAt +
                    " wants=" + PendantManager.Wants(__instance, __instance.pendingMedicine) + " state=" + __instance.pawn.MentalStateDef +
                    " available=" + PendantManager.Available(__instance.pawn) + " holder=" + __instance.pawn.ParentHolder?.GetType() +
                    " reason=" + MedicineCatalog.CanUse(__instance.pendingMedicine, __instance.pawn).Reason);
        }
    }
}
