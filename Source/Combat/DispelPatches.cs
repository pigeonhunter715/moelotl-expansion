using System;
using Axolotl;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoeLotlExpansion
{
    [HarmonyPatch(typeof(Verb), nameof(Verb.WarmupComplete))]
    internal static class DispelBurstStart
    {
        private static void Prefix(Verb __instance) => DispelTracker.Instance?.Begin(__instance.CasterPawn, __instance, true);
    }
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    internal static class DispelShotScope
    {
        private static void Prefix(Verb __instance, out AttackScope __state) =>
            __state = new AttackScope(DispelTracker.Instance?.Begin(__instance.CasterPawn, __instance));
        private static Exception Finalizer(Exception __exception, AttackScope __state)
        { __state?.Finish(__exception == null); return __exception; }
    }
    [HarmonyPatch(typeof(Ability), nameof(Ability.Activate), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    internal static class DispelAbilityScope
    {
        private static void Prefix(Ability __instance, out AttackScope __state) =>
            __state = new AttackScope(DispelTracker.Instance?.Begin(__instance.pawn));
        private static Exception Finalizer(Exception __exception, AttackScope __state)
        { __state?.Finish(__exception == null); return __exception; }
    }
    [HarmonyPatch(typeof(AxolotlUtility.MoelotlEnergyUtility), nameof(AxolotlUtility.MoelotlEnergyUtility.DoEnergyCost))]
    internal static class DispelActualCost
    {
        private static void Postfix(Pawn pawn, float costAmount, bool __result)
        {
            var scope = AttackScope.Current;
            if (__result && costAmount > 0f && scope?.record?.attacker == pawn) scope.paid = true;
        }
    }
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class DispelDirectDamage
    {
        private static void Prefix(Thing __instance, DamageInfo dinfo)
        {
            var scope = AttackScope.Current;
            if (__instance is Pawn target && scope?.record != null && dinfo.Instigator == scope.record.attacker)
                scope.hits.Add(target);
        }
    }
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    internal static class DispelMeleeContact
    {
        private static void Prefix(LocalTargetInfo target, Verb_MeleeAttackDamage __instance)
        {
            var scope = AttackScope.Current;
            if (target.Pawn != null && scope?.record?.attacker == __instance.CasterPawn) scope.hits.Add(target.Pawn);
        }
    }
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef) })]
    internal static class DispelProjectileLaunch
    {
        private static void Postfix(Projectile __instance, Thing launcher)
        {
            var scope = AttackScope.Current;
            if (scope?.record != null && launcher == scope.record.attacker) scope.projectiles.Add(__instance);
        }
    }
    [HarmonyPatch(typeof(Projectile), "Impact")]
    internal static class DispelProjectileContact
    {
        private static void Prefix(Projectile __instance, Thing hitThing, bool blockedByShield)
        {
            if (!blockedByShield && hitThing is Pawn target) DispelTracker.Instance?.Impact(__instance, target);
        }
    }
}
