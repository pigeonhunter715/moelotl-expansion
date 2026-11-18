using System;
using MoeLotlExpansion.Core;

internal static class MedicineRulesTests
{
    private static int checks;
    private static void Equal<T>(T expected, T actual, string name)
    {
        checks++;
        if (!Equals(expected, actual)) throw new Exception(name + ": expected " + expected + ", got " + actual);
    }

    public static int Main()
    {
        try
        {
            Equal(40f, MedicineRules.RecoveryAmount(100f, 0f), "level one recovery");
            Equal(25, MedicineRules.StorageSpace(0,0,0), "per type limit");
            Equal(0, MedicineRules.StorageSpace(60,10,6), "total full");
            Equal(0, MedicineRules.StorageSpace(6,0,6), "seventh type rejected");
            Equal(5, MedicineRules.StorageSpace(55,20,6), "same type fifth remaining");
            Equal(false, MedicineRules.ValidTarget(-1,0,1), "negative target denied");
            Equal(false, MedicineRules.ValidTarget(26,0,1), "target above twenty five denied");
            Equal(false, MedicineRules.ValidTarget(25,36,3), "target total denied");
            Equal(false, MedicineRules.ValidTarget(1,5,7), "reserved seventh type denied");
            Equal(true, MedicineRules.ValidTarget(0,60,6), "zero target leaves stock");
            Equal(0, MedicineRules.RefillNeed(12,7,0,false), "above half no refill");
            Equal(6, MedicineRules.RefillNeed(12,6,0,false), "half triggers exact deficit");
            Equal(2, MedicineRules.RefillNeed(12,6,4,false), "in flight deducted");
            Equal(1, MedicineRules.RefillNeed(1,0,0,false), "single emergency target respected");
            Equal(5, MedicineRules.RefillNeed(12,7,0,true), "manual refill bypasses half threshold");
            Equal(true, MedicineRules.CanDispel(0, false, 0, 100), "first paid contact");
            Equal(false, MedicineRules.CanDispel(1, true, 0, 1000), "same burst cannot repeat after cooldown");
            Equal(false, MedicineRules.CanDispel(0, false, 180, 179), "pair cooldown last tick");
            Equal(true, MedicineRules.CanDispel(0, false, 180, 180), "pair cooldown exact boundary");
            Equal(true, MedicineRules.CanDispel(2, false, 0, 100), "third target allowed");
            Equal(false, MedicineRules.CanDispel(3, false, 0, 100), "sixty base total cap");
            Equal(800f, MedicineRules.ControlledMalice(1200f, 1000f, false, 300f), "purify hard ceiling");
            Equal(0f, MedicineRules.ControlledMalice(0f, 1000f, false, 300f), "purify never fills");
            Equal(300f, MedicineRules.ControlledMalice(0f, 1000f, true, 300f), "pearl preserved");
            Equal(240f, MedicineRules.ControlledMalice(600f, 300f, true, 300f), "ceiling wins floor conflict");
            Equal(0f, MedicineRules.ControlledMalice(0f, 300f, true, 300f), "conflict suppresses rather than replaces floor");
            Equal(300f, MedicineRules.ControlledMalice(0f, 375f, true, 300f), "pearl restored at boundary");
            Equal(150f, MedicineRules.MaliceDose(650f, 1000f, true), "gather limited by purify");
            Equal(250f, MedicineRules.MaliceDose(650f, 1000f, false), "gather normal ninety percent");
            Equal(0f, MedicineRules.MaliceDose(1000f, 1000f, false), "gather never reduces malice");
            Equal(120f, MedicineRules.RecoveryAmount(300f, 0f), "level three recovery");
            Equal(200f, MedicineRules.RecoveryAmount(800f, 0f), "cultivation cap");
            Equal(12f, MedicineRules.RecoveryAmount(300f, 288f), "partial recovery");
            Equal(0f, MedicineRules.RecoveryAmount(300f, 300f), "full qi rejected");
            Equal(0f, MedicineRules.RecoveryAmount(0f, 0f), "missing capacity");
            Equal(0f, MedicineRules.RecoveryAmount(float.NaN, 0f), "invalid qi rejected");
            Equal(15000, MedicineRules.FatigueRemaining(20000, 20000), "six hour fatigue");
            Equal(10000, MedicineRules.FatigueRemaining(20000, 25000), "late expiry preserves elapsed fatigue");
            Equal(0, MedicineRules.FatigueRemaining(20000, 35000), "travel does not restart fatigue");
            Equal(MedicineRejection.InvalidTarget, MedicineRules.CheckLivingTarget(false, false, false, false), "missing pawn");
            Equal(MedicineRejection.InvalidTarget, MedicineRules.CheckLivingTarget(true, false, true, false), "dead MoeLotl");
            Equal(MedicineRejection.RequiresLivingMoeLotl, MedicineRules.CheckLivingTarget(true, true, false, false), "other race");
            Equal(MedicineRejection.RequiresLivingMoeLotl, MedicineRules.CheckLivingTarget(true, true, true, true), "zombie denied");
            Equal(MedicineRejection.None, MedicineRules.CheckLivingTarget(true, true, true, false), "living MoeLotl accepted");

            long expiry = MedicineRules.RenewAt(1000, 120000);
            Equal(120000, MedicineRules.Remaining(expiry, 1000), "two day duration");
            Equal(1, MedicineRules.Remaining(expiry, 120999), "last valid tick");
            Equal(0, MedicineRules.Remaining(expiry, 121000), "expires at exact tick");
            Equal(0, MedicineRules.Remaining(expiry, 500000), "world tick jump");
            Equal(130000L, MedicineRules.RenewAt(10000, 120000), "refresh starts from now, never stacks");
            Equal(60000, MedicineRules.Remaining(MedicineRules.RenewAt(1000, 60000), 1000), "industry one day");
            Equal((long)int.MaxValue + 120000, MedicineRules.RenewAt(int.MaxValue, 120000), "expiry arithmetic avoids int overflow");
            bool rejected = false;
            try { MedicineRules.RenewAt(1, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Equal(true, rejected, "invalid duration rejected");
            Console.WriteLine("PASS: " + checks + " medicine rule checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
