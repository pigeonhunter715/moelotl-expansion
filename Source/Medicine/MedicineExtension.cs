using System.Collections.Generic;
using Verse;

namespace MoeLotlExpansion
{
    public sealed class MedicineExtension : DefModExtension
    {
        public HediffDef effect;
        public int durationTicks;
        public bool requiresMeridians;
        public bool restoresQi;
        public bool preventRefresh;

        public override IEnumerable<string> ConfigErrors()
        {
            if (effect == null) yield return "Medicine effect is missing.";
            else if (!typeof(Hediff_TimedMedicine).IsAssignableFrom(effect.hediffClass))
                yield return "Medicine effect must use Hediff_TimedMedicine.";
            if (durationTicks <= 0) yield return "Medicine duration must be positive.";
        }
    }
}
