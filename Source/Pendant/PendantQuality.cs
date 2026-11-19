using System;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    // Single mapping shared by the native shield multiplier, inspection and tests.
    // QualityCategory order: Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary.
    internal static class PendantQuality
    {
        private const float BaseShieldCount = 16f;

        public static QualityCategory QualityOf(MedicinePendant pendant) =>
            pendant?.TryGetComp<CompQuality>()?.Quality ?? QualityCategory.Normal;

        private static int Index(QualityCategory quality) => Math.Min(Math.Max((int)quality, 0), 6);
        public static int ShieldCapacity(QualityCategory quality) => 8 + 4 * Index(quality);
        public static int DamageReductionPercent(QualityCategory quality) => 3 + 2 * Index(quality);

        public static float ShieldMultiplier(MedicinePendant pendant)
        {
            Pawn wearer = pendant.Wearer;
            if (wearer != null && (wearer.Dead || wearer.IsMutant || MedicinePendant.Active(wearer) != pendant)) return 0f;
            return ShieldCapacity(QualityOf(pendant)) / BaseShieldCount;
        }

        public static float DamageReduction(MedicinePendant pendant) => DamageReductionPercent(QualityOf(pendant)) / 100f;
    }
}
