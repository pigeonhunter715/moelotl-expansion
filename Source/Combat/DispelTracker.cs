using System;
using System.Collections.Generic;
using System.Linq;
using RigorMortis;
using Verse;
using MoeLotlExpansion.Core;

namespace MoeLotlExpansion
{
    public sealed class DispelAction : IExposable
    {
        public Pawn attacker;
        public Verb verb;
        public List<Projectile> projectiles = new List<Projectile>();
        // Admitted target IDs for this action. Keep the existing scribe key for save compatibility.
        public List<int> hitTargets = new List<int>();
        public int createdAt;
        public void ExposeData()
        {
            Scribe_References.Look(ref attacker, "attacker");
            Scribe_References.Look(ref verb, "verb");
            Scribe_Collections.Look(ref projectiles, "projectiles", LookMode.Reference);
            Scribe_Collections.Look(ref hitTargets, "hitTargets", LookMode.Value);
            Scribe_Values.Look(ref createdAt, "createdAt");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                projectiles = projectiles ?? new List<Projectile>();
                hitTargets = hitTargets ?? new List<int>();
            }
        }
    }
    public sealed class DispelCooldown : IExposable
    {
        public int attackerId, targetId, expiresAt;
        public void ExposeData()
        {
            Scribe_Values.Look(ref attackerId, "attackerId");
            Scribe_Values.Look(ref targetId, "targetId");
            Scribe_Values.Look(ref expiresAt, "expiresAt");
        }
    }
    public sealed class DispelTracker : GameComponent
    {
        private List<DispelAction> actions = new List<DispelAction>();
        private List<DispelCooldown> cooldowns = new List<DispelCooldown>();
        public DispelTracker(Game game) { }
        public static DispelTracker Instance => Current.Game?.GetComponent<DispelTracker>();
        public static bool Enabled(Pawn pawn) => pawn != null && !pawn.Dead &&
            ZombieMedicineService.Active(pawn, MXDefOf.MX_DispelEffect);

        public DispelAction Begin(Pawn pawn, Verb verb = null, bool newBurst = false)
        {
            if (!Enabled(pawn)) return null;
            DispelAction record = !newBurst && verb != null ? actions.LastOrDefault(a => a.verb == verb) : null;
            if (newBurst && verb != null)
                foreach (var previous in actions.Where(a => a.verb == verb)) previous.verb = null;
            if (record == null)
            {
                record = new DispelAction { attacker = pawn, verb = verb, createdAt = Find.TickManager.TicksGame };
                actions.Add(record);
            }
            return record;
        }
        public void Impact(Projectile projectile, Pawn target)
        {
            var record = actions.FirstOrDefault(a => a.projectiles.Contains(projectile));
            if (record != null) Hit(record, target);
        }
        public void Hit(DispelAction record, Pawn target)
        {
            if (record == null || !Enabled(record.attacker) || target == null || target.Dead || !target.IsZombie(out _)) return;
            int now = Find.TickManager.TicksGame;
            int targetId = target.thingIDNumber;
            bool admitted = record.hitTargets.Contains(targetId);
            int until = cooldowns.Where(c => c.attackerId == record.attacker.thingIDNumber && c.targetId == targetId)
                .Select(c => c.expiresAt).DefaultIfEmpty(0).Max();
            if (!MedicineRules.CanDispel(admitted, until, now)) return;
            if (!admitted)
            {
                record.hitTargets.Add(targetId);
                cooldowns.Add(new DispelCooldown
                {
                    attackerId = record.attacker.thingIDNumber,
                    targetId = targetId,
                    expiresAt = now + MedicineRules.DispelIntervalTicks
                });
            }
            // Use the dependency's flying-zombie and configured dispel multipliers.
            RMUtility.ChangeMalevolent(target, -MedicineRules.DispelPerHit);
        }
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 600 != 0) return;
            cooldowns.RemoveAll(c => c.expiresAt <= Find.TickManager.TicksGame);
            foreach (var action in actions) action.projectiles.RemoveAll(p => p == null || p.Destroyed);
            actions.RemoveAll(a => a.projectiles.Count == 0 && (a.verb == null || !a.verb.Bursting));
        }
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref actions, "mxDispelActions", LookMode.Deep);
            Scribe_Collections.Look(ref cooldowns, "mxDispelCooldowns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                actions = actions ?? new List<DispelAction>();
                cooldowns = cooldowns ?? new List<DispelCooldown>();
            }
        }
    }

    internal sealed class AttackScope
    {
        [ThreadStatic] public static AttackScope Current;
        public readonly AttackScope previous;
        public readonly DispelAction record;
        public bool paid;
        public readonly List<Pawn> hits = new List<Pawn>();
        public readonly List<Projectile> projectiles = new List<Projectile>();
        public AttackScope(DispelAction record) { this.record = record; previous = Current; Current = this; }
        public void Finish(bool successful)
        {
            Current = previous;
            if (!successful || record == null || !paid) return;
            record.projectiles.AddRange(projectiles.Where(p => p != null && !p.Destroyed));
            foreach (Pawn hit in hits) DispelTracker.Instance.Hit(record, hit);
        }
    }
}
