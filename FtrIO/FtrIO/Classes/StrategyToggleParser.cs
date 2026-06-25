namespace FtrIO.Classes
{
    using System.IO;
    using FtrIO.Interfaces;
    using FtrIO.Strategies;
    using Microsoft.Extensions.Configuration;
    using ToggleExceptions;

    /// <summary>
    /// An IToggleParser that routes raw toggle values through a chain of IToggleDecisionStrategy
    /// implementations before resolving a boolean result. BooleanStrategy is always appended as
    /// the final fallback so existing true/false config values continue to work.
    ///
    /// Two ways to supply values:
    ///   1. appsettings.json (default) — same two-pass reload behaviour as ToggleParser.
    ///   2. IToggleValueProvider — any external source (env vars, HTTP, Azure App Config).
    ///
    /// Usage:
    ///   // appsettings.json with strategies
    ///   new StrategyToggleParser(new PercentageRolloutStrategy(), new BlueGreenStrategy(...))
    ///
    ///   // HTTP source with strategies
    ///   new StrategyToggleParser(new HttpToggleParser("https://..."), new PercentageRolloutStrategy())
    /// </summary>
    public class StrategyToggleParser : IToggleParser
    {
        private readonly IToggleValueProvider? _provider;
        private readonly IToggleDecisionStrategy[] _strategies;
        private readonly OverrideResolver? _overrides;

        // appsettings.json path (used when no IToggleValueProvider)
        private readonly bool _configFileExists;
        private readonly IConfigurationSection? _toggles;
        private readonly IConfigurationSection? _overridesSection;

        public StrategyToggleParser(params IToggleDecisionStrategy[] strategies)
            : this(overrides: null, basePath: null, strategies) { }

        public StrategyToggleParser(OverrideResolver? overrides, params IToggleDecisionStrategy[] strategies)
            : this(overrides, basePath: null, strategies) { }

        public StrategyToggleParser(string? basePath, params IToggleDecisionStrategy[] strategies)
            : this(overrides: null, basePath, strategies) { }

        public StrategyToggleParser(OverrideResolver? overrides, string? basePath, params IToggleDecisionStrategy[] strategies)
        {
            _overrides = overrides;
            _strategies = BuildStrategyChain(strategies);
            basePath ??= AppContext.BaseDirectory;
            _configFileExists = File.Exists(Path.Combine(basePath, "appsettings.json"));

            if (_configFileExists)
            {
                // First pass: read FtrIO settings from the base file only.
                var bootstrapConfiguration = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

                var reloadOnChange = string.Equals(
                    bootstrapConfiguration["FtrIO:ReloadOnChange"], "true", StringComparison.OrdinalIgnoreCase);

                var environment = bootstrapConfiguration["FtrIO:Environment"]
                    ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                    ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

                // Second pass: build the live config with correct reload and env layer.
                var configurationBuilder = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: reloadOnChange);

                if (environment != null)
                    configurationBuilder.AddJsonFile(
                        $"appsettings.{environment}.json", optional: true, reloadOnChange: reloadOnChange);

                var config = configurationBuilder.Build();
                _toggles = config.GetSection("Toggles");
                _overridesSection = config.GetSection("TogglesOverrides");
            }
        }

        public StrategyToggleParser(IToggleValueProvider provider, params IToggleDecisionStrategy[] strategies)
            : this(overrides: null, provider, strategies) { }

        public StrategyToggleParser(OverrideResolver? overrides, IToggleValueProvider provider, params IToggleDecisionStrategy[] strategies)
        {
            _overrides = overrides;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _strategies = BuildStrategyChain(strategies);
            _configFileExists = true;
        }

        public bool GetToggleStatus(string toggle)
        {
            // Overrides win unconditionally before any strategy is consulted.
            if (_overrides is not null)
            {
                var overrideValue = _overrides.GetOverride(toggle);
                if (overrideValue.HasValue) return overrideValue.Value;
            }

            string? rawValue;

            if (_provider != null)
            {
                rawValue = _provider.GetRawValue(toggle);
                if (rawValue == null) throw new ToggleDoesNotExistException();
            }
            else
            {
                if (!_configFileExists) return true;
                rawValue = _toggles?[toggle];
                if (rawValue == null) throw new ToggleDoesNotExistException();
            }

            var matchingStrategy = _strategies.FirstOrDefault(strategy => strategy.CanHandle(rawValue));
            if (matchingStrategy == null) throw new ToggleParsedOutOfRangeException();
            return matchingStrategy.ShouldExecute(toggle, rawValue);
        }

        public bool ParseBoolValueFromSource(string status)
        {
            var matchingStrategy = _strategies.FirstOrDefault(strategy => strategy.CanHandle(status));
            if (matchingStrategy == null) throw new ToggleParsedOutOfRangeException();
            return matchingStrategy.ShouldExecute(string.Empty, status);
        }

        public bool? GetOverride(string toggleKey, string userId)
        {
            var overrideValue = _overridesSection?[$"{toggleKey}:{userId}"];
            if (overrideValue is null) return null;
            if (overrideValue.Equals("true", StringComparison.OrdinalIgnoreCase) || overrideValue == "1") return true;
            if (overrideValue.Equals("false", StringComparison.OrdinalIgnoreCase) || overrideValue == "0") return false;
            return null;
        }

        private static IToggleDecisionStrategy[] BuildStrategyChain(IToggleDecisionStrategy[] strategies)
        {
            var strategyChain = strategies.ToList();
            if (!strategyChain.Any(strategy => strategy is BooleanStrategy))
                strategyChain.Add(new BooleanStrategy());
            return strategyChain.ToArray();
        }
    }
}
