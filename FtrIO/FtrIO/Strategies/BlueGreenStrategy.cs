namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;
    using Microsoft.Extensions.Configuration;

    public class BlueGreenStrategy : IToggleDecisionStrategy
    {
        private readonly IConfiguration? _config;
        private readonly string? _fixedSlot;
        private readonly HashSet<string>? _fixedKnownSlots;

        /// <summary>
        /// Reads CurrentSlot and KnownSlots from appsettings.json under FtrIO:BlueGreen.
        /// Supports ReloadOnChange — editing the file flips the active slot without a restart.
        /// </summary>
        public BlueGreenStrategy() : this(AppContext.BaseDirectory) { }

        public BlueGreenStrategy(string basePath)
        {
            _config = BuildConfig(basePath);
        }

        /// <summary>
        /// Explicit slot values — useful for tests or programmatic wiring.
        /// </summary>
        public BlueGreenStrategy(string currentSlot, params string[] knownSlots)
        {
            _fixedSlot = currentSlot ?? throw new ArgumentNullException(nameof(currentSlot));
            _fixedKnownSlots = new HashSet<string>(knownSlots, StringComparer.OrdinalIgnoreCase);
        }

        private string? CurrentSlot =>
            _config != null
                ? _config["FtrIO:BlueGreen:CurrentSlot"]
                : _fixedSlot;

        private IEnumerable<string> KnownSlots =>
            _config != null
                ? (_config["FtrIO:BlueGreen:KnownSlots"]
                       ?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                   ?? Enumerable.Empty<string>())
                : (IEnumerable<string>?)_fixedKnownSlots ?? Enumerable.Empty<string>();

        public bool CanHandle(string rawValue)
            => KnownSlots.Contains(rawValue.Trim(), StringComparer.OrdinalIgnoreCase);

        public bool ShouldExecute(string toggleKey, string rawValue)
            => string.Equals(rawValue.Trim(), CurrentSlot, StringComparison.OrdinalIgnoreCase);

        private static IConfiguration BuildConfig(string basePath)
        {
            var bootstrapConfiguration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var reloadOnChange = string.Equals(
                bootstrapConfiguration["FtrIO:ReloadOnChange"], "true", StringComparison.OrdinalIgnoreCase);

            var environment = bootstrapConfiguration["FtrIO:Environment"]
                ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: reloadOnChange);

            if (environment != null)
                configurationBuilder.AddJsonFile(
                    $"appsettings.{environment}.json", optional: true, reloadOnChange: reloadOnChange);

            return configurationBuilder.Build();
        }
    }
}
