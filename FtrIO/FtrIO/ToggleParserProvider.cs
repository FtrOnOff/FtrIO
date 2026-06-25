namespace FtrIO
{
    using FtrIO.Classes;
    using FtrIO.Interfaces;

    /// <summary>
    /// Ambient provider for the IToggleParser used by [Toggle] and [ToggleAsync] aspects.
    ///
    /// Defaults to ToggleParser (reads from appsettings.json in AppContext.BaseDirectory).
    /// Override at application startup to inject a custom parser — including one resolved
    /// from a DI container:
    ///
    ///   // Manual:
    ///   ToggleParserProvider.Configure(new ToggleParser("/custom/path"));
    ///
    ///   // With Microsoft.Extensions.DependencyInjection:
    ///   ToggleParserProvider.Configure(serviceProvider.GetRequiredService&lt;IToggleParser&gt;());
    /// </summary>
    public static class ToggleParserProvider
    {
        private static IToggleParser? _instance;

        public static IToggleParser Instance => _instance ??= new ToggleParser();

        public static void Configure(IToggleParser parser)
        {
            _instance = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        /// <summary>
        /// Returns a new ToggleParserBuilder for fluent construction of a StrategyToggleParser.
        /// Call Build() on the result to produce the parser, then pass it to Configure().
        ///
        /// Example:
        ///   ToggleParserProvider.Configure(
        ///       ToggleParserProvider.Builder()
        ///           .WithPercentageRollout()
        ///           .WithBlueGreen()
        ///           .Build()
        ///   );
        /// </summary>
        public static ToggleParserBuilder Builder() => new ToggleParserBuilder();

        /// <summary>
        /// Convenience overload that accepts a builder configuration action and calls
        /// Configure internally. Equivalent to calling Builder(), chaining methods,
        /// then passing the result of Build() to Configure().
        ///
        /// Example:
        ///   ToggleParserProvider.ConfigureBuilder(builder => builder
        ///       .WithContextStrategies(contextAccessor)
        ///       .WithPercentageRollout()
        ///       .WithBlueGreen()
        ///       .WithOverrides(contextAccessor)
        ///   );
        /// </summary>
        public static void ConfigureBuilder(Action<ToggleParserBuilder> configure)
        {
            var builder = new ToggleParserBuilder();
            configure(builder);
            Configure(builder.Build());
        }
    }
}
