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
}
