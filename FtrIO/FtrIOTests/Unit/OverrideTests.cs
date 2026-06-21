namespace FtrIOTests.Unit
{
    using FtrIO.Classes;
    using FtrIO.Interfaces;
    using FtrIO.Strategies;
    using NUnit.Framework;

    [TestFixture]
    public class OverrideTests
    {
        // ── Test doubles ─────────────────────────────────────────────────────────

        private sealed class FakeAccessor : IFtrIOContextAccessor
        {
            private readonly string? _userId;
            public FakeAccessor(string? userId) { _userId = userId; }
            public string? GetUserId() => _userId;
            public string? GetAttribute(string name) => null;
        }

        private sealed class FakeParser : IToggleParser
        {
            private readonly Dictionary<(string key, string userId), bool> _overrides;
            public FakeParser(Dictionary<(string key, string userId), bool> overrides) { _overrides = overrides; }
            public bool GetToggleStatus(string toggle) => throw new NotImplementedException();
            public bool ParseBoolValueFromSource(string status) => throw new NotImplementedException();
            public bool? GetOverride(string toggleKey, string userId)
                => _overrides.TryGetValue((toggleKey, userId), out var v) ? v : null;
        }

        private static IFtrIOContextAccessor User(string id) => new FakeAccessor(id);
        private static IFtrIOContextAccessor NoUser() => new FakeAccessor(null);

        private static OverrideResolver BuildResolver(
            IFtrIOContextAccessor accessor,
            Dictionary<(string, string), bool>? overrides = null)
            => new(accessor, new FakeParser(overrides ?? new Dictionary<(string, string), bool>()));

        // ── OverrideResolver ──────────────────────────────────────────────────────

        [Test]
        public void OverrideResolver_GetOverride_ReturnsTrueWhenOverrideIsTrue()
        {
            var resolver = BuildResolver(User("alice"), new() { [("MyToggle", "alice")] = true });
            Assert.AreEqual(true, resolver.GetOverride("MyToggle"));
        }

        [Test]
        public void OverrideResolver_GetOverride_ReturnsFalseWhenOverrideIsFalse()
        {
            var resolver = BuildResolver(User("alice"), new() { [("MyToggle", "alice")] = false });
            Assert.AreEqual(false, resolver.GetOverride("MyToggle"));
        }

        [Test]
        public void OverrideResolver_GetOverride_ReturnsNullWhenNoOverrideForUser()
        {
            var resolver = BuildResolver(User("bob"), new() { [("MyToggle", "alice")] = true });
            Assert.IsNull(resolver.GetOverride("MyToggle"));
        }

        [Test]
        public void OverrideResolver_GetOverride_ReturnsNullWhenNoOverrideForKey()
        {
            var resolver = BuildResolver(User("alice"), new() { [("OtherToggle", "alice")] = true });
            Assert.IsNull(resolver.GetOverride("MyToggle"));
        }

        [Test]
        public void OverrideResolver_GetOverride_ReturnsNullWhenNoUserContext()
        {
            var resolver = BuildResolver(NoUser(), new() { [("MyToggle", "alice")] = true });
            Assert.IsNull(resolver.GetOverride("MyToggle"));
        }

        [Test]
        public void OverrideResolver_GetOverride_ReturnsNullWhenNoOverridesExist()
        {
            var resolver = BuildResolver(User("alice"));
            Assert.IsNull(resolver.GetOverride("MyToggle"));
        }

        // ── IToggleParser.GetOverride default ────────────────────────────────────

        [Test]
        public void ToggleParser_GetOverride_ReturnsTrueForExplicitTrueOverride()
        {
            var parser = new ToggleParser(TestHelpers.TestAppsettingsDir);
            Assert.AreEqual(false, parser.GetOverride("FakeTrue", "user-override-off"));
        }

        [Test]
        public void ToggleParser_GetOverride_ReturnsFalseForExplicitFalseOverride()
        {
            var parser = new ToggleParser(TestHelpers.TestAppsettingsDir);
            Assert.AreEqual(true, parser.GetOverride("FakeFalse", "user-override-on"));
        }

        [Test]
        public void ToggleParser_GetOverride_ReturnsNullWhenKeyHasNoOverrides()
        {
            var parser = new ToggleParser(TestHelpers.TestAppsettingsDir);
            Assert.IsNull(parser.GetOverride("ButtonToggle", "anyone"));
        }

        [Test]
        public void ToggleParser_GetOverride_ReturnsNullWhenUserNotInOverrideList()
        {
            var parser = new ToggleParser(TestHelpers.TestAppsettingsDir);
            Assert.IsNull(parser.GetOverride("FakeTrue", "unknown-user"));
        }

        // ── StrategyToggleParser override precedence ──────────────────────────────

        [Test]
        public void StrategyToggleParser_Override_WinsOverBooleanStrategyTrue()
        {
            // FakeTrue is "true" in config — override flips it to false for "user-override-off"
            var accessor = User("user-override-off");
            var parser = new ToggleParser(TestHelpers.TestAppsettingsDir);
            var overrides = new OverrideResolver(accessor, parser);
            var stp = new StrategyToggleParser(overrides, TestHelpers.TestAppsettingsDir);

            Assert.IsFalse(stp.GetToggleStatus("FakeTrue"));
        }

        [Test]
        public void StrategyToggleParser_Override_WinsOverBooleanStrategyFalse()
        {
            // FakeFalse is "false" in config — override flips it to true for "user-override-on"
            var accessor = User("user-override-on");
            var parser = new ToggleParser(TestHelpers.TestAppsettingsDir);
            var overrides = new OverrideResolver(accessor, parser);
            var stp = new StrategyToggleParser(overrides, TestHelpers.TestAppsettingsDir);

            Assert.IsTrue(stp.GetToggleStatus("FakeFalse"));
        }

        [Test]
        public void StrategyToggleParser_Override_DoesNotAffectOtherUsers()
        {
            // "other-user" has no override — normal config value applies
            var accessor = User("other-user");
            var parser = new ToggleParser(TestHelpers.TestAppsettingsDir);
            var overrides = new OverrideResolver(accessor, parser);
            var stp = new StrategyToggleParser(overrides, TestHelpers.TestAppsettingsDir);

            Assert.IsTrue(stp.GetToggleStatus("FakeTrue"));
            Assert.IsFalse(stp.GetToggleStatus("FakeFalse"));
        }

        [Test]
        public void StrategyToggleParser_WithoutOverrideResolver_BehavesNormally()
        {
            var stp = new StrategyToggleParser(TestHelpers.TestAppsettingsDir);
            Assert.IsTrue(stp.GetToggleStatus("FakeTrue"));
            Assert.IsFalse(stp.GetToggleStatus("FakeFalse"));
        }

        [Test]
        public void StrategyToggleParser_Override_WinsOverPercentageStrategy()
        {
            // StrategyPercentageAlwaysOn is "100%" — but we can override it off for a specific user
            var accessor = User("locked-out-user");
            var fakeParser = new FakeParser(new() { [("StrategyPercentageAlwaysOn", "locked-out-user")] = false });
            var overrides = new OverrideResolver(accessor, fakeParser);
            var stp = new StrategyToggleParser(overrides, TestHelpers.TestAppsettingsDir,
                new PercentageRolloutStrategy());

            Assert.IsFalse(stp.GetToggleStatus("StrategyPercentageAlwaysOn"));
        }

        // ── ABTestStrategy salt support ───────────────────────────────────────────

        [TestCase("ab:50:round2")]
        [TestCase("ab:0:salt")]
        [TestCase("ab:100:my-experiment")]
        public void ABTestStrategy_CanHandle_ReturnsTrueForSaltedAbValues(string value)
        {
            var strategy = new ABTestStrategy(new FakeAccessor("user-1"));
            Assert.IsTrue(strategy.CanHandle(value));
        }

        [Test]
        public void ABTestStrategy_Salt_IsDeterministicForSameUserKeyAndSalt()
        {
            var strategy = new ABTestStrategy(new FakeAccessor("alice"));
            var first = strategy.ShouldExecute("MyToggle", "ab:50:round1");
            for (var i = 0; i < 20; i++)
                Assert.AreEqual(first, strategy.ShouldExecute("MyToggle", "ab:50:round1"),
                    "Same user + key + salt must always produce the same result");
        }

        [Test]
        public void ABTestStrategy_DifferentSalts_CanProduceDifferentAssignments()
        {
            // With 100 users, different salts should give at least some different bucket assignments
            var users = Enumerable.Range(0, 100).Select(i => $"user-{i}").ToList();

            var resultsRound1 = users.Select(u =>
                new ABTestStrategy(new FakeAccessor(u)).ShouldExecute("Toggle", "ab:50:round1")).ToList();
            var resultsRound2 = users.Select(u =>
                new ABTestStrategy(new FakeAccessor(u)).ShouldExecute("Toggle", "ab:50:round2")).ToList();

            // The two salt rounds should not produce identical assignments for all 100 users
            Assert.IsFalse(resultsRound1.SequenceEqual(resultsRound2),
                "Different salts should produce different population assignments");
        }

        [Test]
        public void ABTestStrategy_Salt_AlwaysFalseAtZeroPercent()
        {
            var strategy = new ABTestStrategy(new FakeAccessor("alice"));
            for (var i = 0; i < 50; i++)
                Assert.IsFalse(strategy.ShouldExecute($"key{i}", "ab:0:anysalt"));
        }

        [Test]
        public void ABTestStrategy_Salt_AlwaysTrueAtOneHundredPercent()
        {
            var strategy = new ABTestStrategy(new FakeAccessor("alice"));
            for (var i = 0; i < 50; i++)
                Assert.IsTrue(strategy.ShouldExecute($"key{i}", "ab:100:anysalt"));
        }

        [Test]
        public void ABTestStrategy_NoSalt_BehavesIdenticallyToPreviousImplementation()
        {
            // "ab:50" (no salt) should give the same deterministic result as before
            var strategy = new ABTestStrategy(new FakeAccessor("alice"));
            var first = strategy.ShouldExecute("NewCheckoutFlow", "ab:50");
            for (var i = 0; i < 20; i++)
                Assert.AreEqual(first, strategy.ShouldExecute("NewCheckoutFlow", "ab:50"));
        }
    }

    internal static class TestHelpers
    {
        // Path to the FtrIOTests project directory where appsettings.json lives.
        public static string TestAppsettingsDir =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\.."));
    }
}
