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
