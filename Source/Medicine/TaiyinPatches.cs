using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RigorMortis;
using RimWorld;
using Verse;

namespace MoeLotlExpansion
{
    // Rigor Mortis cancels Pawn.Kill for immortal zombies. Bypass the guard only for
    // the current failed dose so normal death and corpse handling still run.
    [HarmonyPatch(typeof(CompYinAndMalevolent), nameof(CompYinAndMalevolent.IsImmortal), MethodType.Getter)]
    internal static class TaiyinLethalDose
    {
        [ThreadStatic] private static Pawn recipient;

        internal static void Kill(Pawn pawn)
        {
            Pawn previous = recipient;
            recipient = pawn;
            try { pawn.Kill(null, null); }
            finally { recipient = previous; }
        }

        private static void Postfix(CompYinAndMalevolent __instance, ref bool __result)
        {
            if (recipient != null && __instance.Zombie == recipient) __result = false;
        }
    }

    // Counts only humanlike kills made while the killer is a zombie. The record starts empty on
    // existing saves, so kills from before the pawn became a zombie are never credited.
    [HarmonyPatch(typeof(RecordsUtility), "Notify_PawnKilled")]
    internal static class TaiyinKillRecordPatch
    {
        private static void Postfix(Pawn killed, Pawn killer)
        {
            if (killed == null || killer == null || killed.RaceProps?.Humanlike != true) return;
            if (!killer.IsZombie(out _)) return;
            // Match the original humanlike-kill exclusion for subhumans and anomaly entities.
            if (ModsConfig.AnomalyActive &&
                (killed.IsSubhuman || killed.RaceProps.IsAnomalyEntity)) return;
            RecordDef record = TaiyinMedicine.KillRecord;
            if (record == null) return;
            killer.records?.AddTo(record, 1f);
        }
    }

    // The Anomaly trait is used when available. This fallback patch only skips skill decay for
    // holders of the extension trait, so it cannot affect ordinary pawns or the original trait.
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    internal static class TaiyinPerfectMemoryFallback
    {
        private static bool Prefix(Pawn ___pawn)
        {
            TraitDef fallback = TaiyinMedicine.FallbackPerfectMemory;
            if (fallback == null || ___pawn == null) return true;
            return ___pawn.story?.traits?.HasTrait(fallback) != true;
        }
    }

    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.GetHungerRateFactor))]
    internal static class TaiyinHungerRate
    {
        private static void Postfix(HediffSet __instance, HediffDef ignore, ref float __result)
        {
            if (ignore != TaiyinMedicine.EffectDef && TaiyinMedicine.Active(__instance.pawn))
                __result *= 0.5f;
        }
    }

    // Danger is only the natural over-capacity berserk trigger. Suppressing the getter keeps the
    // current malice, capacity, and every other forced mental state untouched while Taiyin is on.
    [HarmonyPatch(typeof(CompYinAndMalevolent), nameof(CompYinAndMalevolent.Danger), MethodType.Getter)]
    internal static class TaiyinNoOvercapBerserk
    {
        private static void Postfix(CompYinAndMalevolent __instance, ref bool __result)
        {
            if (__result && TaiyinMedicine.Active(__instance.Zombie)) __result = false;
        }
    }

    // RePair reads Props.repairAmountPerDay through exactly one ldfld. Scale that value before
    // the native severity/rate and per-part factors are applied, so all native repair paths
    // (wounds, organs, limbs) receive the same x3 after the final stat calculation.
    [HarmonyPatch(typeof(CompZombieBodyRepair), "RePair")]
    internal static class TaiyinRepairAmount
    {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = instructions.ToList();
            MethodInfo scale = AccessTools.Method(typeof(TaiyinMedicine),
                nameof(TaiyinMedicine.ScaleRepair), new[] { typeof(float), typeof(Pawn) });
            int matches = 0;
            foreach (CodeInstruction instruction in list)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldfld &&
                    instruction.operand is FieldInfo field &&
                    field.Name == "repairAmountPerDay" &&
                    field.FieldType == typeof(float))
                {
                    matches++;
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, scale);
                }
            }
            if (matches != 1)
                Log.Warning("[MoeLotlExpansion] Taiyin repair transpiler matched " +
                    matches + " repairAmountPerDay reads; expected 1.");
        }
    }

    // The only 1800 constant in CompPostTick is the newly-downed repair pause. Replacing that
    // instruction in place preserves labels/exception blocks and every other counter.
    [HarmonyPatch(typeof(CompZombieBodyRepair), nameof(CompZombieBodyRepair.CompPostTick))]
    internal static class TaiyinRepairStoppingTicks
    {
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo stoppingTicks = AccessTools.Method(typeof(TaiyinMedicine),
                nameof(TaiyinMedicine.RepairStoppingTicks), new[] { typeof(CompZombieBodyRepair) });
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4 &&
                    instruction.operand is int value && value == 1800)
                {
                    matches++;
                    instruction.opcode = OpCodes.Ldarg_0;
                    instruction.operand = null;
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, stoppingTicks);
                    continue;
                }
                yield return instruction;
            }
            if (matches != 1)
                Log.Warning("[MoeLotlExpansion] Taiyin repair-pause transpiler matched " +
                    matches + " 1800 constants; expected 1.");
        }
    }

    // The comp overload is the single native choke point used by lightning, attacks and
    // artifacts. Direct setters used by the extension's purification and rescue still work.
    // Positive gains are deliberately left to native scaling, including the new x2 factor.
    [HarmonyPatch(typeof(RMUtility), nameof(RMUtility.ChangeMalevolent),
        new[] { typeof(CompYinAndMalevolent), typeof(float), typeof(bool) })]
    internal static class TaiyinMalevolentDrainImmunity
    {
        private static bool Prefix(CompYinAndMalevolent comp, float num)
        {
            return num >= 0f || !TaiyinMedicine.Active(comp?.Zombie);
        }
    }

    // ApplyDamageToPart is the native point that reads dinfo.IgnoreArmor. Setting it here keeps
    // pre-damage shields and damage reactions ahead of us; only attacks instigated by an active
    // Taiyin bypass armor.
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart",
        new[] { typeof(DamageInfo), typeof(Pawn), typeof(DamageWorker.DamageResult) })]
    internal static class TaiyinArmorPiercing
    {
        private static void Prefix(ref DamageInfo dinfo)
        {
            if (dinfo.Instigator is Pawn attacker && TaiyinMedicine.Active(attacker))
                dinfo.SetIgnoreArmor(true);
        }
    }

    // These getters are the only sources for the water/wood/metal/fire loops in Move. They
    // return fresh lists, so filtering an active Taiyin cannot affect other pawns or the
    // compass's benefits for them. ZombiesInField also contains flying pawns, which must be
    // unwrapped so an airborne Taiyin is exempt from the fire finisher as well.
    [HarmonyPatch(typeof(FiveElementCompass), nameof(FiveElementCompass.PawnsInField),
        MethodType.Getter)]
    internal static class TaiyinCompassPawnsFilter
    {
        private static void Postfix(ref List<Pawn> __result)
        {
            __result?.RemoveAll(TaiyinMedicine.Active);
        }
    }

    [HarmonyPatch(typeof(FiveElementCompass), nameof(FiveElementCompass.ZombiesInField),
        MethodType.Getter)]
    internal static class TaiyinCompassZombiesFilter
    {
        private static void Postfix(ref List<Thing> __result)
        {
            __result?.RemoveAll(IsActiveTaiyin);
        }

        private static bool IsActiveTaiyin(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null && thing is PawnFlyer flyer) pawn = flyer.FlyingPawn;
            return TaiyinMedicine.Active(pawn);
        }
    }

    // The water counter runs on the defender's hediff but reacts to the zombie attacker. Skip
    // only the callback caused by an active Taiyin; ordinary defenders keep the native push,
    // burn and malevolent drain against every other attacker.
    [HarmonyPatch(typeof(HediffWaterField), nameof(HediffWaterField.Notify_PawnPostApplyDamage),
        new[] { typeof(DamageInfo), typeof(float) })]
    internal static class TaiyinWaterFieldCounter
    {
        private static bool Prefix(DamageInfo dinfo)
        {
            return !(dinfo.Instigator is Pawn attacker && TaiyinMedicine.Active(attacker));
        }
    }

    // A preexisting fire field can tick before Hediff_Taiyin's first active cleanup. Suppress
    // its burn/finisher for an active body without altering the field for anyone else.
    [HarmonyPatch(typeof(HediffFireField), nameof(HediffFireField.PostTick))]
    internal static class TaiyinFireFieldGuard
    {
        private static bool Prefix(HediffFireField __instance)
        {
            return !TaiyinMedicine.Active(__instance.pawn);
        }
    }

    [HarmonyPatch(typeof(FiveElementCompass), nameof(FiveElementCompass.Intercepted))]
    internal static class TaiyinCompassFinish
    {
        private static void Postfix(ref Pawn deadman)
        {
            if (TaiyinMedicine.Active(deadman)) deadman = null;
        }
    }

    // Protect a Taiyin who already received the native execution hediff in an older save.
    [HarmonyPatch(typeof(HediffAbility_CompassExecute), nameof(HediffAbility_CompassExecute.PostTick))]
    internal static class TaiyinCompassExecuteGuard
    {
        private static bool Prefix(HediffAbility_CompassExecute __instance)
        {
            if (!TaiyinMedicine.Active(__instance.pawn)) return true;
            if (__instance.pawn.health.hediffSet.hediffs.Contains(__instance))
                __instance.pawn.health.RemoveHediff(__instance);
            return false;
        }
    }
}
