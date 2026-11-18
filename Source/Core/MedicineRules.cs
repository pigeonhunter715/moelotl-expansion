using System;

namespace MoeLotlExpansion.Core
{
    public enum MedicineRejection { None, InvalidTarget, RequiresLivingMoeLotl }

    // Pure rules shared by in-game execution and the executable regression checks.
    public static class MedicineRules
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

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
