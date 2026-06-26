namespace FtrIO.Providers.Http
{
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using FtrIO.Interfaces;

    /// <summary>
    /// Polls an HTTP endpoint for toggle values and stages them to an IToggleBuffer,
    /// which then flushes them to appsettings.json. appsettings.json is the source of
    /// truth for all reads — if the endpoint is unreachable, the last flushed state in
    /// appsettings.json is served automatically (fail-safe, no extra handling needed).
    ///
    /// Expected endpoint response (same JSON shape as appsettings.json):
    ///   { "Toggles": { "SendWelcomeEmail": "true", "NewCheckout": "50%" } }
    ///
    /// Values are staged as raw strings — the strategy chain in StrategyToggleParser
    /// (percentage rollouts, blue-green slots) is applied at read time by ToggleParser,
    /// so the full decision pipeline still works.
    ///
    /// Usage:
    ///   var buffer = new ToggleProviderBuffer();
    ///   new HttpToggleParser("https://flags.example.com/toggles", buffer);
    ///   ToggleParserProvider.ConfigureBuilder(b => b
    ///       .WithPercentageRollout()
    ///       .WithStrategy(new BlueGreenStrategy("blue", "blue", "green")));
    /// </summary>
    public class HttpToggleParser : IDisposable
    {
        private readonly string _url;
        private readonly IToggleBuffer _buffer;
        private readonly HttpClient _client;
        private readonly bool _ownsClient;
        private readonly Timer _timer;

        public HttpToggleParser(
            string url,
            IToggleBuffer buffer,
            TimeSpan? pollInterval = null,
            HttpClient? client = null)
        {
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _ownsClient = client == null;
            _client = client ?? new HttpClient();

            var pollingInterval = pollInterval ?? TimeSpan.FromSeconds(30);
            // Fire immediately (TimeSpan.Zero) so first push happens at startup
            _timer = new Timer(_ => _ = PollAsync(), null, TimeSpan.Zero, pollingInterval);
        }

        private async Task PollAsync()
        {
            try
            {
                using var response = await _client.GetAsync(_url).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var responseStream = await response.Content
                    .ReadAsStreamAsync().ConfigureAwait(false);
                using var togglesDocument = await JsonDocument.ParseAsync(responseStream).ConfigureAwait(false);

                if (!togglesDocument.RootElement.TryGetProperty("Toggles", out var toggles))
                    return; // malformed response — skip, don't discard existing appsettings.json state

                foreach (var toggleProperty in toggles.EnumerateObject())
                    _buffer.Stage(toggleProperty.Name, toggleProperty.Value.ToString());
            }
            catch
            {
                // Provider offline or transient error — last flushed state remains in
                // appsettings.json; no action needed.
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            if (_ownsClient) _client.Dispose();
        }
    }
}
