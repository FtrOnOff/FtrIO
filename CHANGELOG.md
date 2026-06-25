# Changelog

All notable changes to **FtrIO** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Versions **1.0.0–1.1.0** were released from the original repository
> ([`TheScottBot/FtrIO`](https://github.com/TheScottBot/FtrIO)); the project then moved
> to the [`FtrOnOff`](https://github.com/FtrOnOff/FtrIO) organization at 1.1.1. Pre-2.0
> entries are reconstructed from git tags and commit history, so they summarise rather than
> exhaustively list every change.

## [2.0.0] — 2026-06-26

### Breaking changes

- **`StrategyToggleParser` no longer accepts `OverrideResolver` in its constructors.**
  The overloads that took `OverrideResolver?` as the first argument have been replaced
  with overloads that take `IFtrIOContextAccessor?`. `StrategyToggleParser` now constructs
  the `OverrideResolver` internally, so per-user overrides are an implementation detail the
  caller never wires up. `OverrideResolver` itself still exists but is no longer part of the
  public construction surface.
- **`ToggleParserBuilder.WithOverrides(IFtrIOContextAccessor)` is no longer the only overload.**
  A no-argument `WithOverrides()` has been added that reuses the accessor already supplied to
  `WithContextStrategies`, `WithUserTargeting`, `WithABTesting`, or `WithAttributeRules`. The
  explicit `WithOverrides(IFtrIOContextAccessor)` overload remains for the case where no
  context-aware strategy is registered. `WithOverrides()` throws `InvalidOperationException`
  if no accessor is available.

### Added

- **Fluent configuration via `ToggleParserBuilder`.** Build a `StrategyToggleParser` with a
  readable method chain instead of nested constructors. Exposed through
  `ToggleParserProvider.Builder()` and the one-call `ToggleParserProvider.ConfigureBuilder(...)`.
  Methods: `WithUserTargeting`, `WithAttributeRules`, `WithABTesting`, `WithContextStrategies`,
  `WithPercentageRollout`, `WithBlueGreen`, `WithOverrides`, `WithStrategy`, `WithProvider`,
  `WithBasePath`.

### Changed

- **AspectInjector is now pulled in automatically.** Consuming projects no longer need to add
  their own `<PackageReference Include="AspectInjector" />` — FtrIO flows the weaver
  transitively through its NuGet package, so `[Toggle]` on a consumer's own methods is woven
  with no extra reference.

### Migration

**Direct construction** — pass the accessor instead of constructing an `OverrideResolver`:

```csharp
// Before
new StrategyToggleParser(
    new OverrideResolver(contextAccessor, new ToggleParser()),
    new UserTargetingStrategy(contextAccessor),
    new PercentageRolloutStrategy());

// After
new StrategyToggleParser(
    contextAccessor,
    new UserTargetingStrategy(contextAccessor),
    new PercentageRolloutStrategy());
```

**Fluent builder** — drop the repeated accessor when a context strategy is already registered:

```csharp
// Before
ToggleParserProvider.ConfigureBuilder(builder => builder
    .WithContextStrategies(contextAccessor)
    .WithPercentageRollout()
    .WithOverrides(contextAccessor));

// After
ToggleParserProvider.ConfigureBuilder(builder => builder
    .WithContextStrategies(contextAccessor)
    .WithPercentageRollout()
    .WithOverrides());
```

If you call `WithOverrides()` without first registering a context-aware strategy, pass the
accessor explicitly: `WithOverrides(contextAccessor)`.

**Remove the AspectInjector reference** (optional) — consuming projects can delete their
`<PackageReference Include="AspectInjector" ... />`; it now flows transitively from FtrIO.

## [1.1.2] — 2026-06-21

### Added

- Per-user targeting (`UserTargetingStrategy`) — gate a toggle to an explicit user list
  (`"users:alice,bob"`).
- Attribute-based rules (`AttributeRuleStrategy`) — gate by user attributes
  (`"attribute:plan equals premium"`).
- Deterministic A/B test assignment (`ABTestStrategy`) — per-user bucketing with optional salt
  (`"ab:50"`, `"ab:50:round2"`).
- Per-user overrides (`TogglesOverrides` in `appsettings.json`) that win before any strategy.
- Config-driven `BlueGreenStrategy` with hot-reload — the active slot is read from
  `FtrIO:BlueGreen` in `appsettings.json` and can be flipped live with `ReloadOnChange`.

### Fixed

- Windows/Linux path handling when resolving `appsettings.json`.

## [1.1.1] — 2026-06-20

### Added

- Multi-environment support — per-environment `appsettings.{env}.json` overlays, resolved via
  `FtrIO:Environment` (falling back to `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT`).

### Changed

- Project moved to the **FtrOnOff** organization. README and the PlaygroundConsole were
  updated to reference the [FtrIO.Toaster](https://github.com/FtrOnOff/FtrIO.Toaster)
  companion UI.

## [1.1.0] — 2026-06-19

### Added

- Async support — `[ToggleAsync]` and `ExecuteMethodIfToggleOnAsync`.
- Dynamic provider pipeline — `HttpToggleParser`, `AzureAppConfigToggleParser`, and
  `EnvironmentVariableToggleParser`, staged through `ToggleProviderBuffer` and flushed to
  `appsettings.json`; `CompositeToggleParser` for first-wins fallthrough across sources.
- Strategy pipeline (`StrategyToggleParser`) with percentage rollout and a `BooleanStrategy`
  fallback.
- GitHub Pages documentation site.

### Changed

- `ToggleParser` honours `ReloadOnChange` — `appsettings.json` edits are picked up live
  without a restart.

## [1.0.4] — 2026-06-18

### Changed

- Expanded target frameworks to multi-target .NET 6 through .NET 10.

## [1.0.3] — 2026-06-18

_Released as tag `v1.0.03`._

### Added

- Banner and icon branding assets, referenced from the README and NuGet package.

## [1.0.2] — 2026-06-18

_Released as tag `1.0.2` (no `v` prefix)._

### Fixed

- Embed the LICENSE inside the NuGet package; removed a stray committed `.nupkg` and tidied
  `.gitignore`.

## [1.0.1] — 2026-06-18

### Added

- Roslyn analyzer **FTRIO001** — a build-time error when a `[Toggle]` method has no matching
  entry under `Toggles`.
- MIT LICENSE.

### Changed

- README guidance on accessing the custom exception types.

## [1.0.0] — 2026-06-17

First published NuGet release.

### Added

- `[Toggle]` attribute — gates a method by its own name via compile-time IL weaving.
- `appsettings.json` as the toggle store, read through `ToggleParser`.
- Manual control via `ExecuteMethodIfToggleOn`.
- Custom exceptions (`ToggleDoesNotExistException`, `ToggleParsedOutOfRangeException`,
  `ToggleAttributeMissingException`) consolidated into the main project so FtrIO ships as a
  single NuGet package.

[2.0.0]: https://github.com/FtrOnOff/FtrIO/compare/v1.1.2...HEAD
[1.1.2]: https://github.com/FtrOnOff/FtrIO/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/TheScottBot/FtrIO/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/TheScottBot/FtrIO/compare/v1.0.4...v1.1.0
[1.0.4]: https://github.com/TheScottBot/FtrIO/compare/v1.0.03...v1.0.4
[1.0.3]: https://github.com/TheScottBot/FtrIO/compare/1.0.2...v1.0.03
[1.0.2]: https://github.com/TheScottBot/FtrIO/compare/v1.0.1...1.0.2
[1.0.1]: https://github.com/TheScottBot/FtrIO/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/TheScottBot/FtrIO/releases/tag/v1.0.0
