namespace MonoDetour.UnitTests.HookTests;

public static partial class IEnumeratorTests
{
    private static readonly Queue<int> order = [];

    [Fact]
    public static void CanHookIEnumerator()
    {
        order.Clear();

        var m = new MonoDetourManager();
        EnumerateRange.IEnumeratorDetour(Hook_IEnumeratorDetour, m);

        var lib = new LibraryMethods();

        var enumerator = lib.EnumerateRange(4);
        while (enumerator.MoveNext())
            continue;

        Assert.Equal([1, 2, 3, 4], order);
        order.Clear();

        // Now do it with more hooks.

        EnumerateRange.IEnumeratorPrefix(Hook_IEnumeratorPrefix, m);
        EnumerateRange.IEnumeratorPostfix(Hook_IEnumeratorPostfix, m);

        enumerator = lib.EnumerateRange(4);
        while (enumerator.MoveNext())
            continue;

        m.DisposeHooks();
        Assert.Equal([0, 1, 2, 3, 4, 4], order);
    }

    [Fact]
    public static void CanHookIEnumeratorTWhereTisInt()
    {
        order.Clear();

        var m = new MonoDetourManager();
        EnumerateIntRange.IEnumeratorDetour(Hook_IEnumeratorIntDetour, m);

        var lib = new LibraryMethods();

        var enumerator = lib.EnumerateIntRange(4);
        while (enumerator.MoveNext())
            continue;

        Assert.Equal([1, 2, 3, 4], order);
        order.Clear();

        // Now do it with more hooks.

        EnumerateIntRange.IEnumeratorPrefix(Hook_IEnumeratorIntPrefix, m);
        EnumerateIntRange.IEnumeratorPostfix(Hook_IEnumeratorIntPostfix, m);

        enumerator = lib.EnumerateIntRange(4);
        while (enumerator.MoveNext())
            continue;

        m.DisposeHooks();
        Assert.Equal([0, 1, 2, 3, 4, 4], order);
    }

    private static IEnumerator Hook_IEnumeratorDetour(IEnumerator enumerator)
    {
        while (enumerator.MoveNext())
        {
            order.Enqueue((int)enumerator.Current);
            yield return enumerator.Current;
        }
    }

    private static void Hook_IEnumeratorPrefix(IEnumerator enumerator)
    {
        // Remember, enumerator.Current will be null here since we are in a prefix!
        order.Enqueue(0);
    }

    private static void Hook_IEnumeratorPostfix(IEnumerator enumerator)
    {
        var current = (int)enumerator.Current;
        order.Enqueue(current);
    }

    private static IEnumerator<int> Hook_IEnumeratorIntDetour(IEnumerator<int> enumerator)
    {
        while (enumerator.MoveNext())
        {
            order.Enqueue(enumerator.Current);
            yield return enumerator.Current;
        }
    }

    private static void Hook_IEnumeratorIntPrefix(IEnumerator<int> enumerator)
    {
        // Remember, enumerator.Current will be null here since we are in a prefix!
        order.Enqueue(0);
    }

    private static void Hook_IEnumeratorIntPostfix(IEnumerator<int> enumerator)
    {
        var current = enumerator.Current;
        order.Enqueue(current);
    }
}
