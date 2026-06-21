namespace FtrIOTests.Unit
{
    using FtrIO.Interfaces;
    using FtrIO.Strategies;
    using NUnit.Framework;

    [TestFixture]
    public class ContextStrategyTests
    {
        // ── Test double ───────────────────────────────────────────────────────────

        private sealed class FakeAccessor : IFtrIOContextAccessor
        {
            private readonly string? _userId;
            private readonly Dictionary<string, string>? _attributes;

            public FakeAccessor(string? userId, Dictionary<string, string>? attributes = null)
            {
                _userId = userId;
                _attributes = attributes;
            }

            public string? GetUserId() => _userId;
            public string? GetAttribute(string name)
                => _attributes is not null && _attributes.TryGetValue(name, out var v) ? v : null;
        }

        private static IFtrIOContextAccessor WithUser(string userId) => new FakeAccessor(userId);
        private static IFtrIOContextAccessor WithAttr(string name, string value)
            => new FakeAccessor("user-1", new Dictionary<string, string> { [name] = value });
        private static IFtrIOContextAccessor NoContext() => new FakeAccessor(null);

        // ── UserTargetingStrategy — CanHandle ─────────────────────────────────────

        [TestCase("users:alice,bob")]
        [TestCase("users:single")]
        [TestCase("users:alice, bob, charlie")]
        [TestCase("USERS:alice")]
        public void UserTargetingStrategy_CanHandle_ReturnsTrueForUsersPrefix(string value)
            => Assert.IsTrue(new UserTargetingStrategy(NoContext()).CanHandle(value));

        [TestCase("true")]
        [TestCase("20%")]
        [TestCase("ab:50")]
        [TestCase("attribute:plan equals premium")]
        [TestCase("blue")]
        [TestCase("")]
        public void UserTargetingStrategy_CanHandle_ReturnsFalseForNonUsersValues(string value)
            => Assert.IsFalse(new UserTargetingStrategy(NoContext()).CanHandle(value));

        // ── UserTargetingStrategy — ShouldExecute ─────────────────────────────────

        [Test]
        public void UserTargetingStrategy_ShouldExecute_ReturnsTrueWhenUserIsInList()
        {
            var strategy = new UserTargetingStrategy(WithUser("alice"));
            Assert.IsTrue(strategy.ShouldExecute("key", "users:alice,bob,charlie"));
        }

        [Test]
        public void UserTargetingStrategy_ShouldExecute_ReturnsFalseWhenUserIsNotInList()
        {
            var strategy = new UserTargetingStrategy(WithUser("dave"));
            Assert.IsFalse(strategy.ShouldExecute("key", "users:alice,bob,charlie"));
        }

        [Test]
        public void UserTargetingStrategy_ShouldExecute_IsCaseInsensitive()
        {
            var strategy = new UserTargetingStrategy(WithUser("Alice"));
            Assert.IsTrue(strategy.ShouldExecute("key", "users:ALICE,bob"));
        }

        [Test]
        public void UserTargetingStrategy_ShouldExecute_TrimsWhitespaceFromList()
        {
            var strategy = new UserTargetingStrategy(WithUser("alice"));
            Assert.IsTrue(strategy.ShouldExecute("key", "users:alice , bob , charlie"));
        }

        [Test]
        public void UserTargetingStrategy_ShouldExecute_ReturnsFalseWhenNoUserContext()
        {
            var strategy = new UserTargetingStrategy(NoContext());
            Assert.IsFalse(strategy.ShouldExecute("key", "users:alice,bob"));
        }

        [Test]
        public void UserTargetingStrategy_ShouldExecute_ReturnsFalseForEmptyList()
        {
            var strategy = new UserTargetingStrategy(WithUser("alice"));
            Assert.IsFalse(strategy.ShouldExecute("key", "users:"));
        }

        [Test]
        public void UserTargetingStrategy_ShouldExecute_ReturnsFalseForSingleOtherUser()
        {
            var strategy = new UserTargetingStrategy(WithUser("dave"));
            Assert.IsFalse(strategy.ShouldExecute("key", "users:alice"));
        }

        // ── AttributeRuleStrategy — CanHandle ────────────────────────────────────

        [TestCase("attribute:plan equals premium")]
        [TestCase("attribute:country notEquals US")]
        [TestCase("attribute:email startsWith admin")]
        [TestCase("attribute:email endsWith @company.com")]
        [TestCase("attribute:email contains +beta")]
        [TestCase("attribute:plan in premium,enterprise")]
        [TestCase("attribute:country notIn US,CA")]
        [TestCase("ATTRIBUTE:plan EQUALS premium")]
        public void AttributeRuleStrategy_CanHandle_ReturnsTrueForValidAttributeRules(string value)
            => Assert.IsTrue(new AttributeRuleStrategy(NoContext()).CanHandle(value));

        [TestCase("true")]
        [TestCase("20%")]
        [TestCase("ab:50")]
        [TestCase("users:alice")]
        [TestCase("attribute:plan")]
        [TestCase("attribute:plan equalsXYZ premium")]
        [TestCase("")]
        public void AttributeRuleStrategy_CanHandle_ReturnsFalseForNonAttributeRules(string value)
            => Assert.IsFalse(new AttributeRuleStrategy(NoContext()).CanHandle(value));

        // ── AttributeRuleStrategy — ShouldExecute — operators ────────────────────

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_Equals_ReturnsTrueOnMatch()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("plan", "premium"))
                .ShouldExecute("key", "attribute:plan equals premium"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_Equals_ReturnsFalseOnMismatch()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("plan", "free"))
                .ShouldExecute("key", "attribute:plan equals premium"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_Equals_IsCaseInsensitive()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("plan", "PREMIUM"))
                .ShouldExecute("key", "attribute:plan equals premium"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_NotEquals_ReturnsTrueWhenDifferent()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("plan", "free"))
                .ShouldExecute("key", "attribute:plan notEquals premium"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_NotEquals_ReturnsFalseWhenSame()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("plan", "premium"))
                .ShouldExecute("key", "attribute:plan notEquals premium"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_StartsWith_ReturnsTrueOnMatch()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("email", "admin@example.com"))
                .ShouldExecute("key", "attribute:email startsWith admin"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_StartsWith_ReturnsFalseOnMismatch()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("email", "user@example.com"))
                .ShouldExecute("key", "attribute:email startsWith admin"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_EndsWith_ReturnsTrueOnMatch()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("email", "alice@company.com"))
                .ShouldExecute("key", "attribute:email endsWith @company.com"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_EndsWith_ReturnsFalseOnMismatch()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("email", "alice@other.com"))
                .ShouldExecute("key", "attribute:email endsWith @company.com"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_Contains_ReturnsTrueOnMatch()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("email", "alice+beta@example.com"))
                .ShouldExecute("key", "attribute:email contains +beta"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_Contains_ReturnsFalseOnMismatch()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("email", "alice@example.com"))
                .ShouldExecute("key", "attribute:email contains +beta"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_In_ReturnsTrueWhenAttributeInList()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("plan", "enterprise"))
                .ShouldExecute("key", "attribute:plan in premium,enterprise"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_In_ReturnsFalseWhenAttributeNotInList()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("plan", "free"))
                .ShouldExecute("key", "attribute:plan in premium,enterprise"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_In_IsCaseInsensitive()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("plan", "ENTERPRISE"))
                .ShouldExecute("key", "attribute:plan in premium,enterprise"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_NotIn_ReturnsTrueWhenAttributeNotInList()
            => Assert.IsTrue(new AttributeRuleStrategy(WithAttr("country", "IE"))
                .ShouldExecute("key", "attribute:country notIn US,CA"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_NotIn_ReturnsFalseWhenAttributeInList()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("country", "US"))
                .ShouldExecute("key", "attribute:country notIn US,CA"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_ReturnsFalseWhenAttributeNotOnContext()
            => Assert.IsFalse(new AttributeRuleStrategy(NoContext())
                .ShouldExecute("key", "attribute:plan equals premium"));

        [Test]
        public void AttributeRuleStrategy_ShouldExecute_ReturnsFalseForMalformedRule()
            => Assert.IsFalse(new AttributeRuleStrategy(WithAttr("plan", "premium"))
                .ShouldExecute("key", "attribute:plan"));

        // ── ABTestStrategy — CanHandle ────────────────────────────────────────────

        [TestCase("ab:0")]
        [TestCase("ab:50")]
        [TestCase("ab:100")]
        [TestCase("AB:33")]
        public void ABTestStrategy_CanHandle_ReturnsTrueForValidAbValues(string value)
            => Assert.IsTrue(new ABTestStrategy(NoContext()).CanHandle(value));

        [TestCase("ab:101")]
        [TestCase("ab:-1")]
        [TestCase("ab:abc")]
        [TestCase("20%")]
        [TestCase("true")]
        [TestCase("users:alice")]
        [TestCase("")]
        public void ABTestStrategy_CanHandle_ReturnsFalseForInvalidAbValues(string value)
            => Assert.IsFalse(new ABTestStrategy(NoContext()).CanHandle(value));

        // ── ABTestStrategy — ShouldExecute ───────────────────────────────────────

        [Test]
        public void ABTestStrategy_ShouldExecute_AlwaysReturnsFalseAtZeroPercent()
        {
            var strategy = new ABTestStrategy(WithUser("alice"));
            for (var i = 0; i < 100; i++)
                Assert.IsFalse(strategy.ShouldExecute($"key{i}", "ab:0"));
        }

        [Test]
        public void ABTestStrategy_ShouldExecute_AlwaysReturnsTrueAtOneHundredPercent()
        {
            var strategy = new ABTestStrategy(WithUser("alice"));
            for (var i = 0; i < 100; i++)
                Assert.IsTrue(strategy.ShouldExecute($"key{i}", "ab:100"));
        }

        [Test]
        public void ABTestStrategy_ShouldExecute_IsDeterministicForSameUserAndKey()
        {
            var strategy = new ABTestStrategy(WithUser("alice"));
            var first = strategy.ShouldExecute("NewCheckoutFlow", "ab:50");
            for (var i = 0; i < 20; i++)
                Assert.AreEqual(first, strategy.ShouldExecute("NewCheckoutFlow", "ab:50"),
                    "Same user + key should always produce same result");
        }

        [Test]
        public void ABTestStrategy_ShouldExecute_DifferentKeysGiveIndependentAssignments()
        {
            // With enough keys, at least some should differ from each other
            var strategy = new ABTestStrategy(WithUser("alice"));
            var results = Enumerable.Range(0, 50)
                .Select(i => strategy.ShouldExecute($"toggle{i}", "ab:50"))
                .ToList();
            Assert.IsTrue(results.Any(r => r), "Expected at least one true across different keys");
            Assert.IsTrue(results.Any(r => !r), "Expected at least one false across different keys");
        }

        [Test]
        public void ABTestStrategy_ShouldExecute_DifferentUsersCanGetDifferentAssignments()
        {
            var users = Enumerable.Range(0, 100).Select(i => $"user-{i}").ToList();
            var results = users.Select(u =>
                new ABTestStrategy(WithUser(u)).ShouldExecute("FeatureX", "ab:50")).ToList();
            Assert.IsTrue(results.Any(r => r), "Expected at least one user in treatment group");
            Assert.IsTrue(results.Any(r => !r), "Expected at least one user in control group");
        }

        [Test]
        public void ABTestStrategy_ShouldExecute_WithNoUserContextDoesNotThrow()
        {
            var strategy = new ABTestStrategy(NoContext());
            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 20; i++)
                    strategy.ShouldExecute("key", "ab:50");
            });
        }

        [Test]
        public void ABTestStrategy_ShouldExecute_WithNoUserContext_ProducesBothOutcomesAtFiftyPercent()
        {
            var strategy = new ABTestStrategy(NoContext());
            var results = Enumerable.Range(0, 1000)
                .Select(_ => strategy.ShouldExecute("key", "ab:50"))
                .ToList();
            Assert.IsTrue(results.Any(r => r), "Expected at least one true in 1000 probabilistic trials");
            Assert.IsTrue(results.Any(r => !r), "Expected at least one false in 1000 probabilistic trials");
        }

        [Test]
        public void ABTestStrategy_ShouldExecute_SameUserDifferentTogglesMayDiffer()
        {
            // Assignment for a single user across two different keys should be independent
            // (not guaranteed to differ, but the hash inputs differ — just verify no exception)
            var strategy = new ABTestStrategy(WithUser("alice"));
            Assert.DoesNotThrow(() =>
            {
                _ = strategy.ShouldExecute("ToggleA", "ab:50");
                _ = strategy.ShouldExecute("ToggleB", "ab:50");
            });
        }
    }
}
