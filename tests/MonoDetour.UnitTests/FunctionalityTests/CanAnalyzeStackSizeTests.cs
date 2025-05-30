using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;

namespace MonoDetour.UnitTests.FunctionalityTests;

public static class CanAnalyzeStackSizeTests
{
    [Fact]
    public static void CanAnalyzeStackSize()
    {
        var m = DefaultMonoDetourManager.New();
        m.LogFilter = MonoDetourLogger.LogChannel.None;

        m.ILHook(Stub, WriteLogic);
    }

    private static void WriteLogic(ILManipulationInfo info)
    {
        //  1 | IL_004e: ldarg.0
        //  1 | IL_004f: ldfld MonoMod.RuntimeDetour.Detour MonoMod.RuntimeDetour.ILHook/Context::Detour
        //  2 | IL_0054: dup
        //  1 | IL_0055: brtrue.s IL_005a
        //  0 | IL_0057: pop
        //  0 | IL_0058: br.s IL_005f
        // -1 | IL_005a: call System.Void MonoMod.RuntimeDetour.Detour::Dispose()
        //  └── ERROR: Negative stack size; cannot be -1
        //    ¦ │ INFO: Stack imbalance starts at:
        //    ¦ └ -1 | IL_005a: call System.Void MonoMod.RuntimeDetour.Detour::Dispose()
        //    ¦
        //  0 | IL_005f: ldarg.0

        ILWeaver w = new(info);

        w.DefineLabel(out var label1);
        w.DefineLabel(out var label2);

        w.InsertBeforeCurrent(
            w.Create(OpCodes.Ldc_I4_1),
            w.Create(OpCodes.Dup),
            w.Create(OpCodes.Brtrue, label1),
            w.Create(OpCodes.Pop),
            w.Create(OpCodes.Br, label2),
            w.Create(OpCodes.Pop)
        );

        w.MarkLabelTo(w.Previous, label1);
        w.MarkLabelTo(w.Current, label2);

        StackSizeAnalyzer.Analyze(info.Context.Body);
    }

    static void Stub()
    {
        return;
    }
}
