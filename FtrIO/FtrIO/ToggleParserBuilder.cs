namespace FtrIO
{
    using FtrIO.Classes;
    using FtrIO.Interfaces;
    using FtrIO.Strategies;

    /// <summary>
    /// Fluent builder for constructing a StrategyToggleParser and registering it via
    /// ToggleParserProvider.Configure. Reduces multi-line strategy constructor calls
    /// to a readable chain of named methods. BooleanStrategy is always appended
    /// automatically by StrategyToggleParser — do not add it here.
    /// </summary>
    public class ToggleParserBuilder
    {
        private readonly List<IToggleDecisionStrategy> _strategies = new();
        private IFtrIOContextAccessor? _contextAccessor;
        private IFtrIOContextAccessor? _contextAccessorForOverrides;
        private string? _basePath;
        private IToggleValueProvider? _provider;

        /// <summary>
        /// Adds UserTargetingStrategy to the strategy chain.
        /// Enables toggle gating by explicit user ID list ("users:alice,bob").
        /// Requires an IFtrIOContextAccessor that supplies the current user ID.
        /// </summary>
        public ToggleParserBuilder WithUserTargeting(IFtrIOContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _strategies.Add(new UserTargetingStrategy(contextAccessor));
            return this;
        }

        /// <summary>
        /// Adds AttributeRuleStrategy to the strategy chain.
        /// Enables toggle gating by user attribute rules ("attribute:plan equals premium").
        /// Requires an IFtrIOContextAccessor that supplies user attributes.
        /// </summary>
        public ToggleParserBuilder WithAttributeRules(IFtrIOContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _strategies.Add(new AttributeRuleStrategy(contextAccessor));
            return this;
        }

        /// <summary>
        /// Adds ABTestStrategy to the strategy chain.
        /// Enables deterministic per-user A/B bucketing ("ab:50" or "ab:50:round2").
        /// Requires an IFtrIOContextAccessor that supplies the current user ID.
        /// Falls back to probabilistic per-call behaviour when no user context is available.
        /// </summary>
        public ToggleParserBuilder WithABTesting(IFtrIOContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _strategies.Add(new ABTestStrategy(contextAccessor));
            return this;
        }

        /// <summary>
        /// Adds UserTargetingStrategy, AttributeRuleStrategy, and ABTestStrategy to the
        /// strategy chain in a single call, all using the same IFtrIOContextAccessor.
        /// Convenience method for the common case where all context-aware strategies
        /// are used together with the same accessor.
        /// </summary>
        public ToggleParserBuilder WithContextStrategies(IFtrIOContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _strategies.Add(new UserTargetingStrategy(contextAccessor));
            _strategies.Add(new AttributeRuleStrategy(contextAccessor));
            _strategies.Add(new ABTestStrategy(contextAccessor));
            return this;
        }

        /// <summary>
        /// Adds PercentageRolloutStrategy to the strategy chain.
        /// Enables probabilistic percentage rollouts ("20%").
        /// </summary>
        public ToggleParserBuilder WithPercentageRollout()
        {
            _strategies.Add(new PercentageRolloutStrategy());
            return this;
        }

        /// <summary>
        /// Adds BlueGreenStrategy to the strategy chain.
        /// Reads the active slot and known slots from FtrIO:BlueGreen in appsettings.json.
        /// Enables deployment slot gating ("blue" / "green").
        /// </summary>
        public ToggleParserBuilder WithBlueGreen()
        {
            _strategies.Add(new BlueGreenStrategy());
            return this;
        }

        /// <summary>
        /// Enables per-user overrides (TogglesOverrides in appsettings.json) using the
        /// IFtrIOContextAccessor already supplied to WithContextStrategies, WithUserTargeting,
        /// WithABTesting, or WithAttributeRules. Call one of those before this overload so the
        /// accessor doesn't have to be repeated. Overrides are checked before any strategy.
        /// Throws InvalidOperationException if no context accessor has been registered.
        /// </summary>
        public ToggleParserBuilder WithOverrides()
        {
            if (_contextAccessor is null)
                throw new InvalidOperationException(
                    "WithOverrides() requires an IFtrIOContextAccessor. " +
                    "Call WithContextStrategies, WithUserTargeting, WithABTesting, or " +
                    "WithAttributeRules before calling WithOverrides(), or use " +
                    "WithOverrides(contextAccessor) to supply one explicitly.");
            _contextAccessorForOverrides = _contextAccessor;
            return this;
        }

        /// <summary>
        /// Enables per-user overrides using an explicitly supplied IFtrIOContextAccessor.
        /// Use this when WithOverrides() is called without any context-aware strategies,
        /// or when a different accessor is needed for overrides than for strategies.
        /// Overrides are checked before any strategy in the chain.
        /// </summary>
        public ToggleParserBuilder WithOverrides(IFtrIOContextAccessor contextAccessor)
        {
            _contextAccessorForOverrides = contextAccessor;
            return this;
        }

        /// <summary>
        /// Adds a custom IToggleDecisionStrategy to the strategy chain.
        /// Strategies are tried in the order they are added — the first whose
        /// CanHandle returns true wins. BooleanStrategy is always appended last.
        /// </summary>
        public ToggleParserBuilder WithStrategy(IToggleDecisionStrategy strategy)
        {
            _strategies.Add(strategy);
            return this;
        }

        /// <summary>
        /// Sets a custom base path for appsettings.json resolution.
        /// Defaults to AppContext.BaseDirectory when not set, matching
        /// the existing StrategyToggleParser behaviour.
        /// </summary>
        public ToggleParserBuilder WithBasePath(string basePath)
        {
            _basePath = basePath;
            return this;
        }

        /// <summary>
        /// Sets an IToggleValueProvider as the toggle value source instead of
        /// appsettings.json. Use this when toggle values come from an HTTP endpoint,
        /// Azure App Config, or any other IToggleValueProvider implementation.
        /// Mutually exclusive with WithBasePath.
        /// </summary>
        public ToggleParserBuilder WithProvider(IToggleValueProvider provider)
        {
            _provider = provider;
            return this;
        }

        /// <summary>
        /// Constructs the StrategyToggleParser from the current builder state.
        /// Passes strategies to the appropriate StrategyToggleParser constructor
        /// based on whether a provider or base path override has been set.
        /// </summary>
        public StrategyToggleParser Build()
        {
            var strategies = _strategies.ToArray();

            if (_provider != null)
                return _contextAccessorForOverrides != null
                    ? new StrategyToggleParser(_contextAccessorForOverrides, _provider, strategies)
                    : new StrategyToggleParser(_provider, strategies);

            if (_basePath != null)
                return _contextAccessorForOverrides != null
                    ? new StrategyToggleParser(_contextAccessorForOverrides, _basePath, strategies)
                    : new StrategyToggleParser(_basePath, strategies);

            return _contextAccessorForOverrides != null
                ? new StrategyToggleParser(_contextAccessorForOverrides, strategies)
                : new StrategyToggleParser(strategies);
        }
    }
}
