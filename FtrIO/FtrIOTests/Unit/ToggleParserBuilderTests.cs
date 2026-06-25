namespace FtrIOTests.Unit
{
    using FtrIO;
    using FtrIO.Classes;
    using FtrIO.Interfaces;
    using FtrIO.Strategies;
    using NUnit.Framework;

    [TestFixture]
    public class ToggleParserBuilderTests
    {
        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenNoStrategiesAdded()
        {
            var result = new ToggleParserBuilder().Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenPercentageRolloutStrategyAdded()
        {
            var result = new ToggleParserBuilder()
                .WithPercentageRollout()
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenBlueGreenStrategyAdded()
        {
            var result = new ToggleParserBuilder()
                .WithBlueGreen()
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenUserTargetingStrategyAdded()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var result = new ToggleParserBuilder()
                .WithUserTargeting(contextAccessor)
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenAttributeRulesStrategyAdded()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var result = new ToggleParserBuilder()
                .WithAttributeRules(contextAccessor)
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenABTestingStrategyAdded()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var result = new ToggleParserBuilder()
                .WithABTesting(contextAccessor)
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenAllContextStrategiesAddedViaSingleCall()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var result = new ToggleParserBuilder()
                .WithContextStrategies(contextAccessor)
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenCustomStrategyAdded()
        {
            var customStrategy = new CustomStrategyTestDouble();
            var result = new ToggleParserBuilder()
                .WithStrategy(customStrategy)
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenOverridesConfigured()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var result = new ToggleParserBuilder()
                .WithOverrides(contextAccessor)
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuildReturnsStrategyToggleParserWhenFullChainConfigured()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var result = new ToggleParserBuilder()
                .WithContextStrategies(contextAccessor)
                .WithPercentageRollout()
                .WithBlueGreen()
                .WithOverrides(contextAccessor)
                .Build();
            Assert.IsInstanceOf<StrategyToggleParser>(result);
        }

        [Test]
        public void TestBuilderMethodsReturnSameBuilderInstanceToSupportMethodChaining()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var builder = new ToggleParserBuilder();
            var returnedFromWithPercentageRollout = builder.WithPercentageRollout();
            Assert.AreSame(builder, returnedFromWithPercentageRollout);
        }

        [Test]
        public void TestWithUserTargetingReturnsBuilderInstanceToSupportMethodChaining()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var builder = new ToggleParserBuilder();
            var returned = builder.WithUserTargeting(contextAccessor);
            Assert.AreSame(builder, returned);
        }

        [Test]
        public void TestWithBlueGreenReturnsBuilderInstanceToSupportMethodChaining()
        {
            var builder = new ToggleParserBuilder();
            var returned = builder.WithBlueGreen();
            Assert.AreSame(builder, returned);
        }

        [Test]
        public void TestWithOverridesReturnsBuilderInstanceToSupportMethodChaining()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();
            var builder = new ToggleParserBuilder();
            var returned = builder.WithOverrides(contextAccessor);
            Assert.AreSame(builder, returned);
        }

        [Test]
        public void TestToggleParserProviderBuilderReturnsNewToggleParserBuilderInstance()
        {
            var result = ToggleParserProvider.Builder();
            Assert.IsInstanceOf<ToggleParserBuilder>(result);
        }

        [Test]
        public void TestToggleParserProviderBuilderReturnsDifferentInstanceOnEachCall()
        {
            var firstBuilder = ToggleParserProvider.Builder();
            var secondBuilder = ToggleParserProvider.Builder();
            Assert.AreNotSame(firstBuilder, secondBuilder);
        }

        [Test]
        public void TestConfigureBuilderCallsConfigureWithBuiltParser()
        {
            var contextAccessor = new FtrIOContextAccessorTestDouble();

            ToggleParserProvider.ConfigureBuilder(builder => builder
                .WithPercentageRollout()
                .WithBlueGreen());

            Assert.IsInstanceOf<StrategyToggleParser>(ToggleParserProvider.Instance);
        }
    }
}
