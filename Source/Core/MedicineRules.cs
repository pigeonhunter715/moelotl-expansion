using System;

namespace MoeLotlExpansion.Core
{
    public enum MedicineRejection { None, InvalidTarget, RequiresLivingMoeLotl }

    // Pure rules shared by in-game execution and the executable regression checks.
    public static class MedicineRules
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        public static float RecoveryAmount(float capacity, float current)
        {
            if (float.IsNaN(capacity) || float.IsInfinity(capacity) || capacity <= 0 ||
                float.IsNaN(current) || float.IsInfinity(current)) return 0f;
            return Math.Max(0f, Math.Min(Math.Min(capacity * 0.4f, 200f), capacity - Math.Max(0f, current)));
        }

        public static int FatigueRemaining(long combatExpiresAt, int now)
        {
            return Remaining(combatExpiresAt + 6L * TicksPerHour, now);
        }

        public static float ControlledMalice(float value, float capacity, bool pearl, float pearlFloor)
        {
            float ceiling = Math.Max(0f, capacity * 0.8f);
            float floor = pearl && ceiling >= pearlFloor ? pearlFloor : 0f;
            return Math.Max(floor, Math.Min(ceiling, value));
        }

        public static float MaliceDose(float current, float capacity, bool controlled)
        {
            return Math.Max(0f, Math.Min(300f, capacity * (controlled ? 0.8f : 0.9f) - current));
        }

        public static MedicineRejection CheckLivingTarget(bool exists, bool alive, bool moeLotl, bool zombie)
        {
            if (!exists || !alive) return MedicineRejection.InvalidTarget;
            return moeLotl && !zombie ? MedicineRejection.None : MedicineRejection.RequiresLivingMoeLotl;
        }

        public static long RenewAt(int now, int duration)
        {
            if (now < 0) throw new ArgumentOutOfRangeException(nameof(now));
            if (duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            return (long)now + duration;
        }

        public static int Remaining(long expiresAt, int now)
        {
            long remaining = expiresAt - now;
            return remaining <= 0 ? 0 : (int)Math.Min(int.MaxValue, remaining);
        }
    }
}
