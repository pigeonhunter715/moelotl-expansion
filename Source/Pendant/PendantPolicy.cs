using System.Collections.Generic;
using System.Linq;
using Verse;
using MoeLotlExpansion.Core;

namespace MoeLotlExpansion
{
    public enum AutoMedicineMode { Off, Conditional, Maintain }
    public sealed class MedicinePolicy : IExposable
    {
        public ThingDef medicine;
        public int target;
        public AutoMedicineMode mode;
        public bool refillActive;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref medicine, "medicine");
            Scribe_Values.Look(ref target, "target");
            Scribe_Values.Look(ref mode, "mode", AutoMedicineMode.Off);
            Scribe_Values.Look(ref refillActive, "refillActive");
        }
    }
    public sealed class PawnMedicinePolicy : IExposable
    {
        public Pawn pawn;
        public bool autoUse, autoRefill, manualRefill;
        public int growthTarget, gatherPausedUntil, nextOrdinaryTick, coldSince = -1, hotSince = -1, lowRescueWarning = -1;
        public bool pearlPaused;
        public List<MedicinePolicy> entries = new List<MedicinePolicy>();
        public List<ThingDef> selectedSlots = new List<ThingDef>();
        public MedicinePendant pendingPendant;
        public Thing pendingMedicine;
        public int deliveryAt;
        public bool pendingManual;
        public MedicinePolicy For(ThingDef def)
        {
            var entry = entries.FirstOrDefault(e => e.medicine == def);
            if (entry == null) { entry = new MedicinePolicy { medicine = def }; entries.Add(entry); }
            return entry;
        }
        public void CancelDelivery() { pendingPendant = null; pendingMedicine = null; deliveryAt = 0; pendingManual = false; }
        public bool SetTarget(MedicinePendant pendant, ThingDef medicine, int amount)
        {
            if (amount < 0 || amount > 25 || !MedicineCatalog.Allowed(medicine)) return false;
            if (entries.Where(e => e.medicine != medicine).Sum(e => e.target) + amount > 60) return false;
            var types = new HashSet<ThingDef>(pendant.Contents.Select(t => t.def));
            foreach (var entry in entries.Where(e => e.medicine != medicine && e.target > 0)) types.Add(entry.medicine);
            if (amount > 0) types.Add(medicine);
            if (!MedicineRules.ValidTarget(amount, entries.Where(e => e.medicine != medicine).Sum(e => e.target), types.Count)) return false;
            For(medicine).target = amount;
            if (amount == 0 || pendant.Count(medicine) >= amount) For(medicine).refillActive = false;
            return true;
        }
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref autoUse, "autoUse"); Scribe_Values.Look(ref autoRefill, "autoRefill");
            Scribe_Values.Look(ref manualRefill, "manualRefill"); Scribe_Values.Look(ref growthTarget, "growthTarget");
            Scribe_Values.Look(ref gatherPausedUntil, "gatherPausedUntil"); Scribe_Values.Look(ref pearlPaused, "pearlPaused");
            Scribe_Values.Look(ref nextOrdinaryTick, "nextOrdinaryTick");
            Scribe_Values.Look(ref coldSince, "coldSince", -1); Scribe_Values.Look(ref hotSince, "hotSince", -1);
            Scribe_Values.Look(ref lowRescueWarning, "lowRescueWarning", -1);
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            Scribe_Collections.Look(ref selectedSlots, "selectedSlots", LookMode.Def);
            Scribe_References.Look(ref pendingPendant, "pendingPendant"); Scribe_References.Look(ref pendingMedicine, "pendingMedicine");
            Scribe_Values.Look(ref deliveryAt, "deliveryAt"); Scribe_Values.Look(ref pendingManual, "pendingManual");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) entries = entries?.Where(e => e.medicine != null).ToList() ?? new List<MedicinePolicy>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) selectedSlots = selectedSlots ?? new List<ThingDef>();
        }
    }
}
