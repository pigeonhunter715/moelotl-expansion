using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using MoeLotlExpansion.Core;

namespace MoeLotlExpansion
{
    public sealed class MedicinePendant : Apparel, IThingHolder
    {
        private ThingOwner<Thing> contents;
        public MedicinePendant() { contents = new ThingOwner<Thing>(this); }
        public ThingOwner<Thing> Contents => contents;
        public int Total => contents.Sum(t => t.stackCount);
        public int Count(ThingDef def) => contents.Where(t => t.def == def).Sum(t => t.stackCount);
        public float ContentsMass => contents.Sum(t => t.GetStatValue(StatDefOf.Mass) * t.stackCount);
        public float ContentsValue => contents.Sum(t => t.MarketValue * t.stackCount);
        public static MedicinePendant Active(Pawn pawn) => pawn?.apparel?.WornApparel.OfType<MedicinePendant>().FirstOrDefault(p => !p.Destroyed && p.HitPoints > 0);
        public int Space(ThingDef def)
        {
            if (!MedicineCatalog.Allowed(def)) return 0;
            var kinds = new HashSet<ThingDef>(contents.Select(t => t.def));
            var policy = Wearer == null ? null : PendantManager.Instance?.For(Wearer);
            if (policy != null) foreach (var entry in policy.entries.Where(e => e.target > 0)) kinds.Add(entry.medicine);
            if (!kinds.Contains(def) && kinds.Count >= 6) return 0;
            return MedicineRules.StorageSpace(Total, Count(def), contents.Select(t => t.def).Distinct().Count());
        }
        public int Load(Thing item, int count)
        {
            if (item == null || item.Destroyed || count <= 0) return 0;
            int accepted = Math.Min(count, Space(item.def));
            return accepted <= 0 ? 0 : contents.TryAddOrTransfer(item, accepted);
        }
        public ThingOwner GetDirectlyHeldThings() => contents;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, contents);
        public void Unload()
        {
            if (MapHeld != null) contents.TryDropAll(PositionHeld, MapHeld, ThingPlaceMode.Near);
            else if (Wearer?.inventory != null) contents.TryTransferAllToContainer(Wearer.inventory.innerContainer);
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Unload();
            if (contents.Count > 0 && ParentHolder?.GetDirectlyHeldThings() is ThingOwner outer) contents.TryTransferAllToContainer(outer);
            if (contents.Count > 0) contents.ClearAndDestroyContents(mode);
            base.Destroy(mode);
        }
        public override void Notify_Equipped(Pawn pawn)
        { base.Notify_Equipped(pawn); PendantManager.Instance?.For(pawn); }
        public override void Notify_Unequipped(Pawn pawn)
        { PendantManager.Instance?.For(pawn).CancelDelivery(); base.Notify_Unequipped(pawn); }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref contents, "medicineContents", this);
            if (contents == null) contents = new ThingOwner<Thing>(this);
        }
        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            foreach (Gizmo gizmo in base.GetWornGizmos()) yield return gizmo;
            if (Wearer?.Faction == Faction.OfPlayer) yield return new Command_Action
            { defaultLabel = "MX_OpenPendant".Translate(), defaultDesc = "MX_PendantDescription".Translate(), icon = def.uiIcon,
                action = () => Find.WindowStack.Add(new Dialog_MedicinePendant(this)) };
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Spawned && contents.Count > 0) yield return new Command_Action
            { defaultLabel = "MX_Unload".Translate(), defaultDesc = "MX_PendantDescription".Translate(), icon = def.uiIcon, action = Unload };
        }
        public override string GetInspectString() => base.GetInspectString() + "\n" + "MX_StoredMedicine".Translate(Total, contents.Select(t => t.def).Distinct().Count());
    }
}
