using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RigorMortis;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoeLotlExpansion
{
    public sealed class Dialog_MedicinePendant : Window
    {
        private readonly MedicinePendant pendant;
        private readonly Dictionary<ThingDef, string> buffers = new Dictionary<ThingDef, string>();
        private readonly List<ThingDef> slots = new List<ThingDef>();
        private static List<MedicinePolicy> clipboard;
        private static bool copiedUse, copiedRefill;
        private static int copiedGrowth;
        public override Vector2 InitialSize => new Vector2(1000, 680);
        public Dialog_MedicinePendant(MedicinePendant pendant)
        {
            this.pendant = pendant; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
            slots.AddRange(pendant.Contents.Select(t => t.def).Distinct());
            var policy = pendant.Wearer == null ? null : PendantManager.Instance?.For(pendant.Wearer);
            if (policy != null) foreach (var entry in policy.entries.Where(e => e.target > 0 || e.mode != AutoMedicineMode.Off))
                if (!slots.Contains(entry.medicine) && slots.Count < 6) slots.Add(entry.medicine);
            if (policy != null) foreach (var medicine in policy.selectedSlots.Where(MedicineCatalog.Allowed))
                if (!slots.Contains(medicine) && slots.Count < 6) slots.Add(medicine);
            while (slots.Count < 6) slots.Add(null);
        }
        private void Choose(int index, PawnMedicinePolicy policy)
        {
            ThingDef old = slots[index];
            if (old != null && (pendant.Count(old) > 0 || policy.For(old).target > 0)) { Reject("MX_ClearSlotFirst"); return; }
            var choices = MedicineCatalog.All.Where(d => !slots.Contains(d) || d == old).OrderBy(d => d.label).Select(d =>
                new FloatMenuOption(d.LabelCap, () => { if (old != null) policy.For(old).mode = AutoMedicineMode.Off; slots[index] = d; policy.selectedSlots = slots.Where(s => s != null).ToList(); })).ToList();
            choices.Add(new FloatMenuOption("MX_EmptySlot".Translate(), () => { if (old != null) policy.For(old).mode = AutoMedicineMode.Off; slots[index] = null; policy.selectedSlots = slots.Where(s => s != null).ToList(); }));
            Find.WindowStack.Add(new FloatMenu(choices));
        }
        private void Reject(string key = "MX_TargetInvalid") => Messages.Message(key.Translate(), MessageTypeDefOf.RejectInput, false);
        public override void DoWindowContents(Rect rect)
        {
            Pawn pawn = pendant.Wearer;
            if (pawn == null) { Widgets.Label(rect, "MX_WearToConfigure".Translate()); return; }
            var policy = PendantManager.Instance.For(pawn);
            Widgets.Label(new Rect(0, 0, 960, 30), "MX_PendantHeader".Translate(pawn.LabelShort, pendant.Total, pendant.Contents.Select(t => t.def).Distinct().Count(), pendant.GetStatValue(StatDefOf.Mass).ToString("0.00")));
            Widgets.CheckboxLabeled(new Rect(0, 35, 230, 28), "MX_AutoUse".Translate(), ref policy.autoUse);
            Widgets.CheckboxLabeled(new Rect(250, 35, 230, 28), "MX_AutoRefill".Translate(), ref policy.autoRefill);
            if (Widgets.ButtonText(new Rect(500, 35, 130, 28), "MX_RefillNow".Translate())) policy.manualRefill = true;
            if (Widgets.ButtonText(new Rect(640, 35, 130, 28), "MX_Unload".Translate())) { policy.CancelDelivery(); pendant.Unload(); }
            Widgets.Label(new Rect(0, 70, 950, 42), "MX_PendantDefaults".Translate());
            Widgets.Label(new Rect(0, 112, 950, 26), "MX_PendantColumns".Translate());
            float y = 142;
            for (int index = 0; index < 6; index++)
            {
                int slotIndex = index; ThingDef medicine = slots[index];
                if (Widgets.ButtonText(new Rect(0, y, 165, 28), medicine == null ? "MX_ChooseMedicine".Translate() : medicine.LabelCap)) Choose(slotIndex, policy);
                if (medicine == null) { y += 52; continue; }
                var entry = policy.For(medicine);
                TooltipHandler.TipRegion(new Rect(0, y, 165, 28), medicine.description + "\n" + ("MX_Condition_" + medicine.defName).Translate());
                Widgets.Label(new Rect(170, y, 45, 28), pendant.Count(medicine).ToString());
                if (!buffers.ContainsKey(medicine)) buffers[medicine] = entry.target.ToString();
                buffers[medicine] = Widgets.TextField(new Rect(220, y, 38, 26), buffers[medicine]);
                if (Widgets.ButtonText(new Rect(260, y, 42, 26), "MX_Set".Translate()))
                {
                    if (!int.TryParse(buffers[medicine], out int n) || !policy.SetTarget(pendant, medicine, n)) Reject();
                    buffers[medicine] = entry.target.ToString();
                }
                if (Widgets.ButtonText(new Rect(305, y, 28, 26), "−"))
                { if (!policy.SetTarget(pendant, medicine, entry.target - 1)) Reject(); buffers[medicine] = entry.target.ToString(); }
                if (Widgets.ButtonText(new Rect(335, y, 28, 26), "+"))
                { if (!policy.SetTarget(pendant, medicine, entry.target + 1)) Reject(); buffers[medicine] = entry.target.ToString(); }
                if (Widgets.ButtonText(new Rect(375, y, 130, 26), MedicineCatalog.ManualOnly(medicine) ? "MX_ManualOnly".Translate() : ("MX_Mode" + entry.mode).Translate()))
                {
                    if (!MedicineCatalog.ManualOnly(medicine))
                    {
                        entry.mode = entry.mode == AutoMedicineMode.Off ? AutoMedicineMode.Conditional :
                            entry.mode == AutoMedicineMode.Conditional && MedicineCatalog.Maintainable(medicine) ? AutoMedicineMode.Maintain : AutoMedicineMode.Off;
                        if (medicine.defName == MedicineCatalog.Pearl && entry.mode != AutoMedicineMode.Off) policy.pearlPaused = false;
                    }
                }
                if (Widgets.ButtonText(new Rect(515, y, 88, 26), "MX_UseNow".Translate()))
                {
                    var item = pendant.Contents.FirstOrDefault(t => t.def == medicine);
                    var report = item == null ? (AcceptanceReport)"MX_NoMedicine".Translate().ToString() : PendantManager.Instance.Start(pendant, item, true);
                    if (!report.Accepted) Messages.Message(report.Reason ?? "MX_InvalidTarget".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
                }
                if (Widgets.ButtonText(new Rect(610, y, 78, 26), "MX_LoadOne".Translate()))
                {
                    bool found = false;
                    foreach (Pawn worker in pawn.Map?.mapPawns.FreeColonistsSpawned ?? new List<Pawn>())
                    {
                        var job = PendantRestock.FindJob(worker, pendant, medicine, 1);
                        if (job == null) continue;
                        worker.jobs.TryTakeOrderedJob(job, JobTag.Misc); found = true; break;
                    }
                    if (!found) Reject("MX_NoRestockPath");
                }
                string state = medicine.GetModExtension<MedicineExtension>()?.effect is HediffDef effect &&
                    pawn.health.hediffSet.GetFirstHediffOfDef(effect) is Hediff_TimedMedicine timed ? timed.LabelInBrackets : "";
                if (medicine.defName == MedicineCatalog.Pearl && policy.pearlPaused) state = "MX_PearlPaused".Translate();
                if (medicine.defName == "MX_GatherPill" && policy.gatherPausedUntil > Find.TickManager.TicksGame) state = "MX_GatherPaused".Translate();
                Widgets.Label(new Rect(700, y, 245, 28), state);
                Widgets.Label(new Rect(170, y + 27, 780, 24), "MX_SlotDetail".Translate(System.Math.Max(0, entry.target - pendant.Count(medicine)), ("MX_Condition_" + medicine.defName).Translate()));
                y += 52;
            }
            Widgets.Label(new Rect(0, y + 4, 260, 28), "MX_GrowthTarget".Translate(policy.growthTarget));
            if (Widgets.ButtonText(new Rect(270, y + 4, 35, 26), "−")) policy.growthTarget = System.Math.Max(0, policy.growthTarget - 1);
            if (Widgets.ButtonText(new Rect(310, y + 4, 35, 26), "+")) policy.growthTarget = System.Math.Min(5, policy.growthTarget + 1);
            if (Widgets.ButtonText(new Rect(365, y + 4, 120, 26), "MX_CopyPolicy".Translate()))
            {
                clipboard = policy.entries.Select(e => new MedicinePolicy { medicine = e.medicine, target = e.target, mode = e.mode }).ToList();
                copiedUse = policy.autoUse; copiedRefill = policy.autoRefill; copiedGrowth = policy.growthTarget;
            }
            if (Widgets.ButtonText(new Rect(495, y + 4, 120, 26), "MX_PastePolicy".Translate()) && clipboard != null)
            {
                var copy = new PawnMedicinePolicy(); bool valid = true;
                foreach (var e in clipboard)
                { if (!copy.SetTarget(pendant, e.medicine, e.target)) { valid = false; break; } copy.For(e.medicine).mode = e.mode; }
                if (!valid) Reject();
                else
                {
                    policy.entries = copy.entries; policy.autoUse = copiedUse; policy.autoRefill = copiedRefill; policy.growthTarget = copiedGrowth; buffers.Clear();
                    slots.Clear(); slots.AddRange(pendant.Contents.Select(t => t.def).Distinct());
                    foreach (var e in policy.entries.Where(e => e.target > 0 || e.mode != AutoMedicineMode.Off)) if (!slots.Contains(e.medicine) && slots.Count < 6) slots.Add(e.medicine);
                    while (slots.Count < 6) slots.Add(null);
                    policy.selectedSlots = slots.Where(s => s != null).ToList();
                }
            }
            Widgets.Label(new Rect(0, y + 38, 950, 42), !pawn.Spawned ? "MX_TravelPaused".Translate() : "MX_PendantManualHelp".Translate());
            var malice = pawn.TryGetComp<CompYinAndMalevolent>();
            if (malice != null && pawn.IsZombie(out _)) Widgets.Label(new Rect(0, y + 82, 950, 30), "MX_MaliceStatus".Translate(malice.Malevolent.ToString("0"), malice.MalevolentCapacity.ToString("0"),
                (malice.MalevolentCapacity * (ZombieMedicineService.Active(pawn, MXDefOf.MX_PurifyEffect) ? 0.8f : 1f)).ToString("0"), ZombieMedicineService.GatherAmount(pawn).ToString("0")));
        }
    }
}
