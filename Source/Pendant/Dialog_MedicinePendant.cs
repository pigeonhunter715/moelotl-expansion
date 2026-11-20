using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RigorMortis;
using UnityEngine;
using Verse;

namespace MoeLotlExpansion
{
    public sealed class Dialog_MedicinePendant : Window
    {
        private const float RowHeight = 60f;
        private const float RowInset = 6f;
        private const float ControlHeight = 26f;
        private const float ColumnGap = 6f;

        private readonly MedicinePendant pendant;
        private readonly Dictionary<ThingDef, string> buffers = new Dictionary<ThingDef, string>();
        private readonly List<ThingDef> slots = new List<ThingDef>();
        private static List<MedicinePolicy> clipboard;
        private static bool copiedUse, copiedRefill;
        private static int copiedGrowth;
        private Vector2 scrollPosition;
        public override Vector2 InitialSize => new Vector2(1000, 680);

        public Dialog_MedicinePendant(MedicinePendant pendant)
        {
            this.pendant = pendant; doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
            Pawn wearer = pendant.Wearer;
            var policy = wearer == null ? null : PendantManager.Instance?.For(wearer);
            slots.AddRange(pendant.Contents.Select(t => t.def).Distinct());
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
            var manager = PendantManager.Instance;
            if (manager == null) { Widgets.Label(rect, "MX_WearToConfigure".Translate()); return; }
            var policy = manager.For(pawn);

            bool showTravel = !pawn.Spawned;
            var malice = pawn.TryGetComp<CompYinAndMalevolent>();
            bool showMalice = malice != null && pawn.IsZombie(out _);

            const float headerHeight = 26f;
            const float toolbarHeight = 30f;
            const float columnHeaderHeight = 24f;
            const float toolbarGap = 8f;
            float rowsTop = headerHeight + toolbarGap + toolbarHeight + toolbarGap + columnHeaderHeight + 4f;
            float rowsBottom = rowsTop + RowHeight * 6f;
            float growthY = rowsBottom + 6f;
            float footerY = growthY + 30f;
            if (showTravel) footerY += 22f;
            float contentHeight = footerY + (showMalice ? 26f : 0f) + 6f;

            Rect outRect = new Rect(0f, 0f, rect.width, Mathf.Max(1f, rect.height - 44f));
            float viewWidth = Mathf.Max(620f, outRect.width - 16f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            try
            {
                float width = viewRect.width;
                var columns = new PendantColumns(width);
                float y = 0f;
                DrawHeader(new Rect(RowInset, y, width - RowInset * 2f, headerHeight), pawn);
                y += headerHeight + toolbarGap;
                DrawToolbar(new Rect(0f, y, width, toolbarHeight), policy);
                y += toolbarHeight + toolbarGap;
                DrawColumnHeader(new Rect(0f, y, width, columnHeaderHeight), columns);
                y += columnHeaderHeight + 4f;
                for (int index = 0; index < 6; index++)
                {
                    DrawRow(new Rect(0f, y, width, RowHeight), index, slots[index], policy, columns, pawn);
                    y += RowHeight;
                }
                DrawGrowthAndPolicyActions(new Rect(0f, y + 6f, width, 26f), policy);
                DrawFooter(new Rect(0f, y + 36f, width, 26f), pawn, showTravel, showMalice, malice);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawHeader(Rect rect, Pawn pawn)
        {
            DrawLabel(rect, "MX_PendantHeader".Translate(pawn.LabelShort, pendant.Total,
                pendant.Contents.Select(t => t.def).Distinct().Count()), TextAnchor.MiddleLeft, GameFont.Small);
            TooltipHandler.TipRegion(rect, "MX_PendantDefaults".Translate() + "\n\n" + "MX_PendantManualHelp".Translate());
        }

        private void DrawToolbar(Rect rect, PawnMedicinePolicy policy)
        {
            float gap = 8f;
            float width = Mathf.Max(1f, (rect.width - RowInset * 2f - gap * 3f) / 4f);
            float x = rect.x + RowInset;
            float y = rect.y + 2f;
            Rect useRect = new Rect(x, y, width, ControlHeight);
            Rect refillRect = new Rect(useRect.xMax + gap, y, width, ControlHeight);
            Rect refillNowRect = new Rect(refillRect.xMax + gap, y, width, ControlHeight);
            Rect unloadRect = new Rect(refillNowRect.xMax + gap, y, width, ControlHeight);

            Widgets.CheckboxLabeled(useRect, "MX_AutoUse".Translate(), ref policy.autoUse);
            TooltipHandler.TipRegion(useRect, "MX_PendantDefaults".Translate());
            Widgets.CheckboxLabeled(refillRect, "MX_AutoRefill".Translate(), ref policy.autoRefill);
            TooltipHandler.TipRegion(refillRect, "MX_PendantDefaults".Translate());
            if (Widgets.ButtonText(refillNowRect, "MX_RefillNow".Translate())) policy.manualRefill = true;
            if (Widgets.ButtonText(unloadRect, "MX_Unload".Translate())) { policy.CancelDelivery(); pendant.Unload(); }
        }

        private void DrawColumnHeader(Rect rect, PendantColumns columns)
        {
            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.065f));
            DrawLabel(columns.Medicine(rect.y, rect.height), "MX_ColumnMedicine".Translate(), TextAnchor.MiddleCenter, GameFont.Small);
            DrawLabel(columns.Count(rect.y, rect.height), "MX_ColumnCount".Translate(), TextAnchor.MiddleCenter, GameFont.Small);
            DrawLabel(columns.Refill(rect.y, rect.height), "MX_ColumnRefillTarget".Translate(), TextAnchor.MiddleCenter, GameFont.Small);
            DrawLabel(columns.Mode(rect.y, rect.height), "MX_ColumnMode".Translate(), TextAnchor.MiddleCenter, GameFont.Small);
            DrawLabel(columns.Actions(rect.y, rect.height), "MX_ColumnActions".Translate(), TextAnchor.MiddleCenter, GameFont.Small);
            DrawLabel(columns.Status(rect.y, rect.height), "MX_ColumnStatus".Translate(), TextAnchor.MiddleLeft, GameFont.Small);
        }

        private void DrawRow(Rect row, int index, ThingDef medicine, PawnMedicinePolicy policy, PendantColumns columns, Pawn pawn)
        {
            Widgets.DrawBoxSolid(new Rect(row.x, row.y, row.width, RowHeight - 2f),
                new Color(1f, 1f, 1f, index % 2 == 0 ? 0.035f : 0.07f));
            Widgets.DrawBoxSolid(new Rect(row.x, row.y + RowHeight - 2f, row.width, 1f), new Color(1f, 1f, 1f, 0.10f));

            float top = row.y + 4f;
            Rect medicineRect = columns.Medicine(top, ControlHeight);
            if (Widgets.ButtonText(medicineRect, medicine == null ? "MX_ChooseMedicine".Translate() : medicine.LabelCap))
                Choose(index, policy);
            if (medicine == null) return;

            var entry = policy.For(medicine);
            TooltipHandler.TipRegion(medicineRect, medicine.description + "\n" + ("MX_Condition_" + medicine.defName).Translate());
            DrawLabel(columns.Count(top, ControlHeight), pendant.Count(medicine).ToString(), TextAnchor.MiddleCenter, GameFont.Small);

            Rect refillRect = columns.Refill(top, ControlHeight);
            float innerWidth = Mathf.Max(1f, refillRect.width - 4f);
            float gap = 4f;
            float stepWidth = Mathf.Clamp(innerWidth * 0.14f, 20f, 30f);
            float fieldWidth = Mathf.Clamp(innerWidth * 0.25f, 34f, 54f);
            float setWidth = Mathf.Clamp(innerWidth * 0.22f, 32f, 48f);
            float total = fieldWidth + setWidth + stepWidth * 2f + gap * 3f;
            if (total > innerWidth)
            {
                float scale = innerWidth / total;
                fieldWidth *= scale; setWidth *= scale; stepWidth *= scale; gap *= scale;
            }
            float fieldX = refillRect.center.x - Mathf.Min(total, innerWidth) / 2f;
            Rect fieldRect = new Rect(fieldX, top, fieldWidth, ControlHeight);
            Rect setRect = new Rect(fieldRect.xMax + gap, top, setWidth, ControlHeight);
            Rect minusRect = new Rect(setRect.xMax + gap, top, stepWidth, ControlHeight);
            Rect plusRect = new Rect(minusRect.xMax + gap, top, stepWidth, ControlHeight);

            if (!buffers.ContainsKey(medicine)) buffers[medicine] = entry.target.ToString();
            buffers[medicine] = Widgets.TextField(fieldRect, buffers[medicine]);
            if (ControlButton(setRect, "MX_Set".Translate()))
            {
                if (!int.TryParse(buffers[medicine], out int n) || !policy.SetTarget(pendant, medicine, n)) Reject();
                buffers[medicine] = entry.target.ToString();
            }
            if (ControlButton(minusRect, "−"))
            { if (!policy.SetTarget(pendant, medicine, entry.target - 1)) Reject(); buffers[medicine] = entry.target.ToString(); }
            if (ControlButton(plusRect, "+"))
            { if (!policy.SetTarget(pendant, medicine, entry.target + 1)) Reject(); buffers[medicine] = entry.target.ToString(); }

            Rect modeRect = columns.Mode(top, ControlHeight);
            string modeLabel = MedicineCatalog.ManualOnly(medicine) ? "MX_ManualOnly".Translate() : ("MX_Mode" + entry.mode).Translate();
            TooltipHandler.TipRegion(modeRect, modeLabel);
            if (ControlButton(modeRect, modeLabel) && !MedicineCatalog.ManualOnly(medicine))
            {
                entry.mode = entry.mode == AutoMedicineMode.Off ? AutoMedicineMode.Conditional :
                    entry.mode == AutoMedicineMode.Conditional && MedicineCatalog.Maintainable(medicine) ? AutoMedicineMode.Maintain : AutoMedicineMode.Off;
                if (medicine.defName == MedicineCatalog.Pearl && entry.mode != AutoMedicineMode.Off) policy.pearlPaused = false;
            }

            Rect actionsRect = columns.Actions(top, ControlHeight);
            float actionGap = 4f;
            float actionWidth = Mathf.Max(1f, (actionsRect.width - actionGap) / 2f);
            Rect useRect = new Rect(actionsRect.x, top, actionWidth, ControlHeight);
            Rect loadRect = new Rect(useRect.xMax + actionGap, top, actionWidth, ControlHeight);
            if (ControlButton(useRect, "MX_UseNow".Translate()))
            {
                var item = pendant.Contents.FirstOrDefault(t => t.def == medicine);
                var report = item == null ? (AcceptanceReport)"MX_NoMedicine".Translate().ToString() : PendantManager.Instance.Start(pendant, item, true);
                if (!report.Accepted) Messages.Message(report.Reason ?? "MX_InvalidTarget".Translate().ToString(), MessageTypeDefOf.RejectInput, false);
            }
            if (ControlButton(loadRect, "MX_LoadOne".Translate()) && !PendantRestock.TryOrderLoadOne(pendant, medicine))
                Reject("MX_NoRestockPath");

            string state = medicine.GetModExtension<MedicineExtension>()?.effect is HediffDef effect &&
                pawn.health.hediffSet.GetFirstHediffOfDef(effect) is Hediff_TimedMedicine timed ? timed.LabelInBrackets : "";
            if (medicine.defName == MedicineCatalog.Pearl && policy.pearlPaused) state = "MX_PearlPaused".Translate();
            if (medicine.defName == "MX_GatherPill" && policy.gatherPausedUntil > Find.TickManager.TicksGame) state = "MX_GatherPaused".Translate();
            Rect statusRect = columns.Status(top, ControlHeight);
            DrawLabel(statusRect, state, TextAnchor.MiddleLeft, GameFont.Tiny);
            if (!string.IsNullOrEmpty(state)) TooltipHandler.TipRegion(statusRect, state);

            string condition = ("MX_Condition_" + medicine.defName).Translate();
            string detail = "MX_SlotDetail".Translate(Mathf.Max(0, entry.target - pendant.Count(medicine)), condition);
            Rect detailRect = new Rect(row.x + RowInset, row.y + 34f, row.width - RowInset * 2f, RowHeight - 38f);
            DrawLabel(detailRect, detail, TextAnchor.MiddleLeft, GameFont.Tiny);
            TooltipHandler.TipRegion(detailRect, detail);
        }

        private void DrawGrowthAndPolicyActions(Rect rect, PawnMedicinePolicy policy)
        {
            float x = rect.x + RowInset;
            float labelWidth = Mathf.Clamp(rect.width * 0.40f, 180f, 300f);
            DrawLabel(new Rect(x, rect.y, labelWidth, ControlHeight), "MX_GrowthTarget".Translate(policy.growthTarget),
                TextAnchor.MiddleLeft, GameFont.Small);
            float buttonX = x + labelWidth + 6f;
            if (ControlButton(new Rect(buttonX, rect.y, 30f, ControlHeight), "−"))
                policy.growthTarget = Mathf.Max(0, policy.growthTarget - 1);
            buttonX += 34f;
            if (ControlButton(new Rect(buttonX, rect.y, 30f, ControlHeight), "+"))
                policy.growthTarget = Mathf.Min(5, policy.growthTarget + 1);

            float copyWidth = Mathf.Clamp(rect.width * 0.14f, 90f, 130f);
            float right = rect.xMax - RowInset;
            Rect pasteRect = new Rect(right - copyWidth, rect.y, copyWidth, ControlHeight);
            Rect copyRect = new Rect(pasteRect.x - copyWidth - 8f, rect.y, copyWidth, ControlHeight);
            if (ControlButton(copyRect, "MX_CopyPolicy".Translate()))
            {
                clipboard = policy.entries.Select(e => new MedicinePolicy { medicine = e.medicine, target = e.target, mode = e.mode }).ToList();
                copiedUse = policy.autoUse; copiedRefill = policy.autoRefill; copiedGrowth = policy.growthTarget;
            }
            if (ControlButton(pasteRect, "MX_PastePolicy".Translate()) && clipboard != null)
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
        }

        private void DrawFooter(Rect rect, Pawn pawn, bool showTravel, bool showMalice, CompYinAndMalevolent malice)
        {
            float y = rect.y;
            if (showTravel)
            {
                Rect travelRect = new Rect(rect.x + RowInset, y, rect.width - RowInset * 2f, 22f);
                string travel = "MX_TravelPaused".Translate();
                DrawLabel(travelRect, travel, TextAnchor.MiddleLeft, GameFont.Tiny);
                TooltipHandler.TipRegion(travelRect, travel);
                y += 22f;
            }
            if (showMalice)
            {
                string status = "MX_MaliceStatus".Translate(malice.Malevolent.ToString("0"), malice.MalevolentCapacity.ToString("0"),
                    (malice.MalevolentCapacity * (ZombieMedicineService.Active(pawn, MXDefOf.MX_PurifyEffect) ? 0.8f : 1f)).ToString("0"),
                    ZombieMedicineService.GatherAmount(pawn).ToString("0"));
                Rect maliceRect = new Rect(rect.x + RowInset, y, rect.width - RowInset * 2f, 26f);
                DrawLabel(maliceRect, status, TextAnchor.MiddleLeft, GameFont.Tiny);
                TooltipHandler.TipRegion(maliceRect, status);
            }
        }

        private static void DrawLabel(Rect rect, string text, TextAnchor anchor, GameFont font)
        {
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            try
            {
                Text.Anchor = anchor;
                Text.Font = font;
                Widgets.Label(rect, text);
            }
            finally
            {
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
        }

        private static bool ControlButton(Rect rect, string label)
        {
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                return Widgets.ButtonText(rect, label);
            }
            finally
            {
                Text.Font = oldFont;
            }
        }

        private readonly struct PendantColumns
        {
            private readonly float x;
            private readonly float medicineWidth;
            private readonly float countWidth;
            private readonly float refillWidth;
            private readonly float modeWidth;
            private readonly float actionsWidth;
            private readonly float statusWidth;
            private readonly float gap;

            public PendantColumns(float width)
            {
                float usable = Mathf.Max(1f, width - RowInset * 2f);
                gap = ColumnGap;
                countWidth = Mathf.Max(42f, usable * 0.07f);
                refillWidth = Mathf.Max(130f, usable * 0.23f);
                modeWidth = Mathf.Max(76f, usable * 0.13f);
                actionsWidth = Mathf.Max(112f, usable * 0.19f);
                statusWidth = Mathf.Max(82f, usable * 0.14f);
                medicineWidth = Mathf.Max(100f, usable - countWidth - refillWidth - modeWidth - actionsWidth - statusWidth - gap * 5f);
                float total = medicineWidth + countWidth + refillWidth + modeWidth + actionsWidth + statusWidth + gap * 5f;
                if (total > usable)
                {
                    float scale = usable / total;
                    medicineWidth *= scale; countWidth *= scale; refillWidth *= scale;
                    modeWidth *= scale; actionsWidth *= scale; statusWidth *= scale; gap *= scale;
                }
                x = RowInset;
            }

            public Rect Medicine(float y, float height) => new Rect(x, y, medicineWidth, height);
            public Rect Count(float y, float height) => new Rect(x + medicineWidth + gap, y, countWidth, height);
            public Rect Refill(float y, float height) => new Rect(x + medicineWidth + countWidth + gap * 2f, y, refillWidth, height);
            public Rect Mode(float y, float height) => new Rect(x + medicineWidth + countWidth + refillWidth + gap * 3f, y, modeWidth, height);
            public Rect Actions(float y, float height) => new Rect(x + medicineWidth + countWidth + refillWidth + modeWidth + gap * 4f, y, actionsWidth, height);
            public Rect Status(float y, float height) => new Rect(x + medicineWidth + countWidth + refillWidth + modeWidth + actionsWidth + gap * 5f, y, statusWidth, height);
        }
    }
}
