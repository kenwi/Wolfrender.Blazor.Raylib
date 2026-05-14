using System.Numerics;

namespace Game.Console;

public static class ConsoleSelfTests
{
    private static bool _hasRun;

    public static void RunOnce()
    {
        if (_hasRun)
            return;
        _hasRun = true;

        TestParserQuotedArgs();
        TestStringVariableStore();
        TestRuntimeAccessorGetSet();
    }

    private static void TestParserQuotedArgs()
    {
        var parser = new ConsoleCommandParser();
        var result = parser.TryParse("set Player.Position \"1, 2, 3\"", out var invocation);
        if (!result.Success || invocation == null)
            throw new InvalidOperationException("Console parser failed to parse quoted arguments.");

        if (!string.Equals(invocation.Name, "set", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Console parser command name mismatch.");
        if (invocation.Arguments.Count != 2)
            throw new InvalidOperationException("Console parser argument count mismatch.");
    }

    private static void TestStringVariableStore()
    {
        var store = new StringVariableStore();
        if (!store.TrySetValue("test.value", "abc", out _))
            throw new InvalidOperationException("StringVariableStore set failed.");
        if (!store.TryGetValue("test.value", out var value, out _))
            throw new InvalidOperationException("StringVariableStore get failed.");
        if (value != "abc")
            throw new InvalidOperationException("StringVariableStore roundtrip mismatch.");
    }

    private static void TestRuntimeAccessorGetSet()
    {
        var target = new TestTarget { MoveSpeed = 5f, Position = new Vector3(1, 2, 3) };
        var accessor = new RuntimeVariableAccessor(
            new[]
            {
                new RootBinding
                {
                    Name = "Test",
                    Resolver = index => index.HasValue
                        ? ResolveResult.Fail("Index not supported")
                        : ResolveResult.Ok(target),
                    CuratedListFactory = () => new[] { "Test.MoveSpeed", "Test.Position" },
                    DiscoveryFactory = () => new[] { "Test.MoveSpeed", "Test.Position" }
                }
            },
            new[] { "Test.MoveSpeed", "Test.Position" });

        if (!accessor.TrySetValue("Test.MoveSpeed", "9.5", out _))
            throw new InvalidOperationException("Runtime accessor failed to set float.");
        if (!accessor.TryGetValue("Test.MoveSpeed", out var moveSpeed, out _))
            throw new InvalidOperationException("Runtime accessor failed to get float.");
        if (moveSpeed != "9.5")
            throw new InvalidOperationException("Runtime accessor float roundtrip mismatch.");

        if (!accessor.TrySetValue("Test.Position", "4,5,6", out _))
            throw new InvalidOperationException("Runtime accessor failed to set Vector3.");
        if (!accessor.TryGetValue("Test.Position", out var position, out _))
            throw new InvalidOperationException("Runtime accessor failed to get Vector3.");
        if (position != "4,5,6")
            throw new InvalidOperationException("Runtime accessor vector roundtrip mismatch.");
    }

    private sealed class TestTarget
    {
        public float MoveSpeed { get; set; }
        public Vector3 Position { get; set; }
    }
}
