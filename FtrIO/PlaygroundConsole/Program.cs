using FtrIO;
using FtrIO.Interfaces;
using System.Text.Json;

// ── Simulated context accessor ────────────────────────────────────────────────
var accessor = new SimulatedContextAccessor();

// ── Strategy pipeline + overrides ────────────────────────────────────────────
// Built fluently via ToggleParserBuilder. Strategy order is preserved exactly:
// the context-aware strategies (user targeting → attribute rules → A/B) come
// first, then percentage rollout, then blue/green. Overrides are checked before
// any strategy in the chain.
ToggleParserProvider.ConfigureBuilder(builder => builder
    .WithContextStrategies(accessor)
    .WithPercentageRollout()
    .WithBlueGreen()        // slot read from FtrIO:BlueGreen:CurrentSlot in appsettings.json
    .WithOverrides(accessor));

// ── Startup banner ────────────────────────────────────────────────────────────
var baseDir = AppContext.BaseDirectory;
var baseFile = Path.Combine(baseDir, "appsettings.json");

string? activeEnv = null;
using (var doc = JsonDocument.Parse(File.ReadAllText(baseFile)))
{
    if (doc.RootElement.TryGetProperty("FtrIO", out var ftrio)
        && ftrio.TryGetProperty("Environment", out var envEl))
        activeEnv = envEl.GetString();
}

Console.WriteLine(new string('=', 74));
Console.WriteLine("FtrIO PlaygroundConsole — [Toggle] in action");
Console.WriteLine(new string('=', 74));
Console.WriteLine($"Base file : {baseFile}");
if (activeEnv is { Length: > 0 })
    Console.WriteLine($"Overlay   : appsettings.{activeEnv}.json");
Console.WriteLine();
Console.WriteLine("Cycling through 4 users every 2s. Edit appsettings.json live. Ctrl+C to exit.");
Console.WriteLine(new string('-', 74));

// ── Users ─────────────────────────────────────────────────────────────────────
var users = new[]
{
    new UserContext("alice",   new Dictionary<string, string> { ["plan"] = "premium", ["country"] = "IE" }),
    new UserContext("bob",     new Dictionary<string, string> { ["plan"] = "free",    ["country"] = "US" }),
    new UserContext("charlie", new Dictionary<string, string> { ["plan"] = "free",    ["country"] = "GB" }),
    new UserContext("dave",    new Dictionary<string, string> { ["plan"] = "premium", ["country"] = "US" }),
};

// ── Main loop ─────────────────────────────────────────────────────────────────
var demo = new ToggleDemo();
var idx = 0;

while (true)
{
    var user = users[idx % users.Length];
    accessor.SetContext(user.Id, user.Attributes);
    idx++;

    Console.WriteLine();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]  User: {user.Id,-10}  plan={user.Attributes["plan"],-12}  country={user.Attributes["country"]}");
    Console.WriteLine($"  {"Toggle",-20}  {"Strategy",-26}  State");
    Console.WriteLine("  " + new string('-', 54));

    demo.Run();

    Thread.Sleep(2000);
}

// ── Simulated context types ───────────────────────────────────────────────────
internal sealed class UserContext
{
    public string Id { get; }
    public Dictionary<string, string> Attributes { get; }
    public UserContext(string id, Dictionary<string, string> attributes) { Id = id; Attributes = attributes; }
}

internal sealed class SimulatedContextAccessor : IFtrIOContextAccessor
{
    private string? _userId;
    private Dictionary<string, string> _attributes = new Dictionary<string, string>();

    public void SetContext(string userId, Dictionary<string, string> attributes)
    {
        _userId = userId;
        _attributes = attributes;
    }

    public string? GetUserId() => _userId;
    public string? GetAttribute(string name)
        => _attributes.TryGetValue(name, out var v) ? v : null;
}

// ── Demo class — every method is a real [Toggle]-gated call ──────────────────
internal class ToggleDemo
{
    private bool _ran;

    // Calls the [Toggle] method; prints OFF line if the aspect suppressed it.
    private void Show(Action method, string key, string strategy)
    {
        _ran = false;
        method();
        if (!_ran)
        {
            Console.WriteLine($"  {key,-20}  {strategy,-26}  \x1b[90mOFF\x1b[0m");
        }
    }

    public void Run()
    {
        Show(TestingTrue,         "TestingTrue",         "BooleanStrategy");
        Show(TestingFalse,        "TestingFalse",        "BooleanStrategy");
        Show(TestingPercentage,   "TestingPercentage",   "PercentageRollout");
        Show(TestingBlueGreen,    "TestingBlueGreen",    "BlueGreenStrategy");
        Show(TestingUserTarget,   "TestingUserTarget",   "UserTargetingStrategy");
        Show(TestingAttribute,    "TestingAttribute",    "AttributeRuleStrategy");
        Show(TestingABTest,       "TestingABTest",       "ABTestStrategy");
        Show(TestingABTestSalted, "TestingABTestSalted", "ABTestStrategy (salted)");
        Show(TestingNoAttribute,  "TestingNoAttribute",  "(no [Toggle])");
    }

    public void TestingOne()
    {
        TestingTrue();
        TestingABTest();
        TestingPercentage();
    }

    [Toggle]
    private void TestingTrue()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingTrue",-20}  {"BooleanStrategy",-26}  \x1b[32mON \x1b[0m  base: true | override: bob=false");
    }

    [Toggle]
    private void TestingFalse()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingFalse",-20}  {"BooleanStrategy",-26}  \x1b[32mON \x1b[0m  overlay: true, base: false");
    }

    [Toggle]
    private void TestingPercentage()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingPercentage",-20}  {"PercentageRollout",-26}  \x1b[32mON \x1b[0m  overlay: 80%, base: 50% — random per call");
    }

    [Toggle]
    private void TestingBlueGreen()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingBlueGreen",-20}  {"BlueGreenStrategy",-26}  \x1b[32mON \x1b[0m  slot: blue");
    }

    [Toggle]
    private void TestingUserTarget()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingUserTarget",-20}  {"UserTargetingStrategy",-26}  \x1b[32mON \x1b[0m  users: alice, charlie");
    }

    [Toggle]
    private void TestingAttribute()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingAttribute",-20}  {"AttributeRuleStrategy",-26}  \x1b[32mON \x1b[0m  attribute: plan equals premium");
    }

    [Toggle]
    private void TestingABTest()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingABTest",-20}  {"ABTestStrategy",-26}  \x1b[32mON \x1b[0m  ab:50 | override: alice=true always");
    }

    [Toggle]
    private void TestingABTestSalted()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingABTestSalted",-20}  {"ABTestStrategy (salted)",-26}  \x1b[32mON \x1b[0m  ab:50:round2 — independent bucket");
    }

    // No [Toggle] — always runs, demonstrates the baseline
    private void TestingNoAttribute()
    {
        _ran = true;
        Console.WriteLine($"  {"TestingNoAttribute",-20}  {"(no [Toggle])",-26}  \x1b[32mON \x1b[0m  always executes");
    }
}
