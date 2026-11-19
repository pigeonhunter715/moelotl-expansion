using Axolotl;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    // The native comp computes GetTrueShieldCount as LotlQiShieldCount * QualityNum.
    // Only this pendant's quality map may change that multiplier; duplicates contribute zero.
    [HarmonyPatch(typeof(CompApparel_LotlQiShield), nameof(CompApparel_LotlQiShield.QualityNum), MethodType.Getter)]
    internal static class PendantQualityNum
    {
        private static void Postfix(CompApparel_LotlQiShield __instance, ref float __result)
        {
            if (__instance?.parent is MedicinePendant pendant)
                __result = PendantQuality.ShieldMultiplier(pendant);
        }
    }

    // Apply after shield checks, before injury and armor resolution.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    internal static class PendantDamageReduction
    {
        private static void Postfix(Pawn __instance, ref DamageInfo dinfo, bool absorbed)
        {
            if (absorbed || __instance == null || __instance.Dead) return;
            if (dinfo.Def == null || dinfo.Def == DamageDefOf.EMP || dinfo.IgnoreArmor) return;
            if (dinfo.Amount <= 0f || !dinfo.Def.ExternalViolenceFor(__instance)) return;
            MedicinePendant pendant = MedicinePendant.Active(__instance);
            if (pendant == null) return;
            float reduction = PendantQuality.DamageReduction(pendant);
            dinfo.SetAmount(dinfo.Amount * (1f - reduction));
        }
    }
}
