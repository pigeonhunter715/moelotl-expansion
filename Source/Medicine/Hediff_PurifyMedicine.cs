using RigorMortis;
using Verse;
namespace MoeLotlExpansion
{
    public sealed class Hediff_PurifyMedicine : Hediff_TimedMedicine
    {
        public override string TipStringExtra
        {
            get
            {
                var comp = pawn.TryGetComp<CompYinAndMalevolent>();
                if (comp == null) return base.TipStringExtra;
                float capacity = comp.MalevolentCapacity;
                string text = "MX_PurifyStatus".Translate(comp.Malevolent.ToString("0.#"), capacity.ToString("0.#"),
                    (capacity * 0.8f).ToString("0.#")).ToString();
                if (capacity * 0.8f < CompYinAndMalevolent.PearlMinMalevolent &&
                    pawn.health.hediffSet.HasHediff(RMDefOf.RM_HediffYakshaPearl))
                    text += "\n" + "MX_PearlFloorSuppressed".Translate();
                return text;
            }
        }
    }
}
