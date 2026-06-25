namespace FtrIO.Classes
{
    using System.Collections.Concurrent;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using FtrIO.Interfaces;

    /// <summary>
    /// Buffers toggle value updates from providers and flushes them to appsettings.json
    /// atomically after a configurable interval. appsettings.json is always the on-disk
    /// source of truth — if a provider goes offline, the last flushed state persists there
    /// and ToggleParser continues serving from it.
    ///
    /// Thread safety:
    ///   Staging uses a ConcurrentDictionary — any number of providers can stage concurrently.
    ///   Rapid successive updates to the same key are collapsed: the last write before the
    ///   next flush wins. File writes are serialised with a lock; if a write is already in
    ///   progress when the timer fires, that tick is skipped (staged values are not lost —
    ///   they accumulate until the next flush succeeds).
    ///
    /// Configuration (appsettings.json):
    ///   "FtrIO": {
    ///     "ReloadOnChange": true,   // recommended so ToggleParser sees each flush live
    ///     "FlushInterval": 5        // seconds between flushes, default 5
    ///   }
    ///
    /// Atomic write: staged values are written to a .tmp file then replaced atomically,
    /// so a crash mid-write never leaves a corrupt appsettings.json.
    ///
    /// Dispose() performs a final flush so no staged changes are lost on shutdown.
    /// </summary>
    public class ToggleProviderBuffer : IToggleBuffer, IDisposable
    {
        private readonly string _settingsPath;
        private readonly ConcurrentDictionary<string, string> _pending
            = new(StringComparer.Ordinal);
        private readonly object _writeLock = new();
        private readonly Timer _timer;
        private volatile bool _disposing;

        public ToggleProviderBuffer(string? basePath = null, TimeSpan? flushInterval = null)
        {
            basePath ??= AppContext.BaseDirectory;

            var environment = ResolveEnvironment(basePath);
            var settingsFileName = environment != null
                ? $"appsettings.{environment}.json"
                : "appsettings.json";
            _settingsPath = Path.Combine(basePath, settingsFileName);

            var interval = flushInterval ?? ReadFlushIntervalFromConfig(basePath);
            _timer = new Timer(_ => TimerFlush(), null, interval, interval);
        }

        /// <inheritdoc />
        public void Stage(string key, string rawValue)
            => _pending[key] = rawValue;

        /// <summary>
        /// Flush all staged changes to appsettings.json immediately.
        /// Safe to call concurrently — only one flush runs at a time.
        /// </summary>
        public void FlushNow() => FlushCore();

        public void Dispose()
        {
            _disposing = true;
            _timer.Dispose();
            FlushCore(); // final flush — don't lose staged changes on shutdown
        }

        private void TimerFlush()
        {
            if (_disposing) return;
            FlushCore();
        }

        private void FlushCore()
        {
            if (_pending.IsEmpty) return;

            // Try to acquire the write lock without blocking the timer thread.
            // If a flush is already running, this tick is skipped — pending changes
            // remain in the ConcurrentDictionary and are picked up next tick.
            if (!Monitor.TryEnter(_writeLock)) return;
            try
            {
                // Drain the staging dict. Keys added by providers between now and the
                // end of the write will appear in the next flush, not this one.
                var togglesToWrite = DrainPending();
                if (togglesToWrite.Count == 0) return;

                try
                {
                    var existingJson = File.Exists(_settingsPath)
                        ? File.ReadAllText(_settingsPath, Encoding.UTF8)
                        : "{}";

                    var updatedJson = MergeToggles(existingJson, togglesToWrite);
                    WriteAtomically(updatedJson);
                }
                catch
                {
                    // Write failed — re-stage the values so the next flush retries them.
                    // TryAdd: preserves any newer value that arrived during the failed write.
                    foreach (var pendingEntry in togglesToWrite)
                        _pending.TryAdd(pendingEntry.Key, pendingEntry.Value);
                }
            }
            finally
            {
                Monitor.Exit(_writeLock);
            }
        }

        private Dictionary<string, string> DrainPending()
        {
            var drained = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in _pending.Keys.ToArray())
                if (_pending.TryRemove(key, out var value))
                    drained[key] = value;
            return drained;
        }

        private void WriteAtomically(string json)
        {
            var tempPath = _settingsPath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            if (File.Exists(_settingsPath))
                File.Replace(tempPath, _settingsPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, _settingsPath);
        }

        private static string MergeToggles(string existingJson, Dictionary<string, string> updates)
        {
            using var existingDocument = JsonDocument.Parse(existingJson,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

            using var outputStream = new MemoryStream();
            using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            var togglesWritten = false;
            foreach (var rootProperty in existingDocument.RootElement.EnumerateObject())
            {
                if (rootProperty.Name == "Toggles")
                {
                    togglesWritten = true;
                    writer.WritePropertyName("Toggles");
                    WriteTogglesSection(writer, rootProperty.Value, updates);
                }
                else
                {
                    rootProperty.WriteTo(writer);
                }
            }

            // appsettings.json had no Toggles section yet — append one
            if (!togglesWritten)
            {
                writer.WritePropertyName("Toggles");
                writer.WriteStartObject();
                foreach (var update in updates)
                    writer.WriteString(update.Key, update.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(outputStream.ToArray());
        }

        private static void WriteTogglesSection(
            Utf8JsonWriter writer,
            JsonElement existing,
            Dictionary<string, string> updates)
        {
            writer.WriteStartObject();
            var writtenToggleNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var existingToggle in existing.EnumerateObject())
            {
                writer.WritePropertyName(existingToggle.Name);
                if (updates.TryGetValue(existingToggle.Name, out var updatedValue))
                    writer.WriteStringValue(updatedValue);
                else
                    existingToggle.Value.WriteTo(writer); // preserves original JSON type (bool, string, etc.)
                writtenToggleNames.Add(existingToggle.Name);
            }

            // Keys from providers that don't yet exist in appsettings.json
            foreach (var update in updates)
                if (!writtenToggleNames.Contains(update.Key))
                    writer.WriteString(update.Key, update.Value);

            writer.WriteEndObject();
        }

        // The buffer only writes to an env-specific file when FtrIO:Environment is
        // explicitly set in appsettings.json. Environment variables (ASPNETCORE_ENVIRONMENT
        // etc.) are intentionally not checked here: on a server where that variable is set
        // for unrelated reasons (e.g. prod has ASPNETCORE_ENVIRONMENT=Production), the buffer
        // must still write to appsettings.json — the server's own file is its environment.
        private static string? ResolveEnvironment(string basePath)
        {
            var settingsFilePath = Path.Combine(basePath, "appsettings.json");
            if (!File.Exists(settingsFilePath)) return null;

            try
            {
                using var settingsDocument = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
                if (settingsDocument.RootElement.TryGetProperty("FtrIO", out var ftrioSection)
                    && ftrioSection.TryGetProperty("Environment", out var environmentElement)
                    && environmentElement.GetString() is { Length: > 0 } environmentName)
                    return environmentName;
            }
            catch { }

            return null;
        }

        private static TimeSpan ReadFlushIntervalFromConfig(string basePath)
        {
            var settingsFilePath = Path.Combine(basePath, "appsettings.json");
            if (!File.Exists(settingsFilePath)) return TimeSpan.FromSeconds(5);

            try
            {
                using var settingsDocument = JsonDocument.Parse(File.ReadAllText(settingsFilePath));
                if (settingsDocument.RootElement.TryGetProperty("FtrIO", out var ftrioSection)
                    && ftrioSection.TryGetProperty("FlushInterval", out var flushIntervalElement)
                    && flushIntervalElement.TryGetInt32(out var flushIntervalSeconds))
                    return TimeSpan.FromSeconds(flushIntervalSeconds);
            }
            catch { }

            return TimeSpan.FromSeconds(5);
        }
    }
}
