# Source Layer Architecture

This document records the source-derived runtime layers as responsibilities, not project names. For the historical project inventory, see [Solution Structure](solution-structure.md). Term definitions live in the [glossary](glossary.md).

## Lifecycle

`App` builds UI/platform services, initializes Toast and single-instance behavior, then starts `BeaconRuntime`. `BeaconRuntime.Start` loads persisted configuration before constructing any pipeline object, preventing empty in-memory configuration from being written back. After startup, App subscribes aggregate and widget events, creates surfaces on demand, starts the refresh host, installs sleep/resume refresh, hotkeys, and topmost guards, and disposes the runtime on exit.

Implementation: `src/Beacon.App/App.xaml.cs:122`, `src/Beacon.App/Services/BeaconRuntime.cs:104`, `src/Beacon.App/Services/BeaconRuntime.cs:111`, `src/Beacon.App/App.xaml.cs:210`, `src/Beacon.App/App.xaml.cs:256`, `src/Beacon.App/Services/BeaconRuntime.cs:279`  
Configuration schema: `src/Beacon.Core/Models/AppConfig.cs:4`, `src/Beacon.Core/Models/WidgetConfig.cs:4`, `src/Beacon.Core/Models/ConnectionConfig.cs:4`  
Protocol files: none for process startup; `BEACON_DEBUG_CRASH` and `BEACON_DEBUG_SEVERITY` are gated diagnostic inputs.  
Copy: board and tray wording is read through `src/Beacon.Core/Services/UiStrings.cs:12`.

## Configuration, Secret, Cache, and Shell Persistence

This layer separates durable user configuration, durable notification history, last-known data, protected credentials, and UI-only shell geometry. Configuration files are atomic and recoverable; credentials never enter JSON; cache loss is non-fatal; shell geometry has its own file. `AppConfigFile` remains deliberately separate for early theme/hotkey reads.

Implementation: `src/Beacon.Storage/JsonConfigurationStore.cs:13`, `src/Beacon.Storage/ImportExport.cs:27`, `src/Beacon.Storage/JsonCacheStore.cs:9`, `src/Beacon.Storage/DpapiSecretStore.cs:12`, `src/Beacon.Storage/JsonNotificationRecordStore.cs:12`, `src/Beacon.App/Services/ShellStateStore.cs:8`, `src/Beacon.Storage/AppConfigFile.cs:8`  
Schema: `AppConfig`, `ConnectionConfig`, `WidgetConfig`, `ActionConfig`, pins, and component trees are defined in `src/Beacon.Core/Models`; the five configuration files are named at `src/Beacon.Storage/JsonConfigurationStore.cs:160`.  
Protocol files: JSON plus `secrets.bin`; malformed files are copied to `*.corrupt` before fallback.  
Copy: user-facing configuration text is built in `src/Beacon.App/Windows/SettingsWindow.cs`; Core strings that cross board/tray use `UiStrings`.

## Collection and Normalization

Persisted widget configuration is registered into the scheduler. The scheduler groups work and calls `WidgetHost`; the host resolves a widget provider, injects credentials, drops stale results after configuration changes, writes the cache, publishes connection health and widget state, and reconstructs stale state on provider failure. Generic and vendor providers translate endpoint/session/local data into one `WidgetState`. The optional DSL adds mapping and action defaults without taking instance override rights.

Implementation: `src/Beacon.Core/Services/RefreshScheduler.cs:17`, `src/Beacon.Core/Services/RefreshScheduler.cs:41`, `src/Beacon.Core/Services/WidgetHost.cs:13`, `src/Beacon.Core/Services/WidgetHost.cs:196`, `src/Beacon.Core/Services/WidgetHost.cs:240`, `src/Beacon.Core/Abstractions/IConnectionProvider.cs:12`, `src/Beacon.Core/Abstractions/IConnectionProvider.cs:29`, `src/Beacon.Connections/ProviderDsl.cs:15`  
Schema: provider-specific fields are advertised through `WidgetTypeDescriptor` implementations; examples include `src/Beacon.Connections/GitHubWidgetDescriptors.cs:20` and `src/Beacon.Connections/BigModelUsageProvider.cs:25`. The DSL file keys are defined at `src/Beacon.Connections/ProviderDsl.cs:18`.  
Protocol files: provider-specific REST/JSON, local files/processes, and the optional DSL YAML. There is no cross-provider wire protocol.  
Copy: providers return provider-specific messages in `WidgetState.Summary`; shared display wording is centralized as listed above.

## In-Process State and Notifications

The normalized state flows through the synchronous process bus. `StateStore` deduplicates unchanged content and exposes snapshots. `StatusAggregator` folds states and connection health into overall status. `NotificationEngine` evaluates rules and keeps records; Toast is only an output sink, while record persistence is a separate store. `WidgetHost` also subscribes to action completion to refresh the source state.

Implementation: `src/Beacon.Core/Abstractions/IEventBus.cs:3`, `src/Beacon.Core/Services/EventBus.cs:6`, `src/Beacon.Core/Services/StateStore.cs:14`, `src/Beacon.Core/Services/StatusAggregator.cs:11`, `src/Beacon.Core/Services/NotificationEngine.cs:14`, `src/Beacon.Core/Abstractions/INotificationSink.cs:5`, `src/Beacon.App/Services/ToastNotificationService.cs:20`, `src/Beacon.Core/Services/WidgetHost.cs:67`  
Schema: event contracts are in `src/Beacon.Core/Events/Events.cs:5`; notification rules and records are in `src/Beacon.Core/Models/NotificationModels.cs`; user rules live in `AppConfig.NotificationRules` at `src/Beacon.Core/Models/AppConfig.cs:49`.  
Protocol files: none for the in-process stream; notifications.json is persistence, not a transport protocol.  
Copy: notification and board empty-state copy is sourced through `UiStrings` and notification provider state.

## Intent and Side Effects

L3 detail and declarative action buttons select built-in actions and DSL actions, confirm unsafe actions in the window, and call `ActionRunner`. The runner routes to an executor, enforces timeout and optional retry, publishes `ActionExecuted`, and thereby asks the data pipeline to refresh the originating widget. Webhook signing uses DPAPI and fails closed without a secret.

Implementation: `src/Beacon.App/Windows/DetailWindow.cs:285`, `src/Beacon.App/Windows/DetailWindow.cs:373`, `src/Beacon.Core/Services/ActionRunner.cs:18`, `src/Beacon.Core/Services/ActionRunner.cs:49`, `src/Beacon.Actions/LocalCommandExecutor.cs:11`, `src/Beacon.Actions/WebhookExecutor.cs:16`, `src/Beacon.Connections/GitHubWorkflowExecutors.cs:69`  
Schema: `src/Beacon.Core/Models/ActionModels.cs:6`; registry action names and retry keys are defined by `src/Beacon.Core/Services/ActionRunner.cs:87`; DSL action vocabulary is at `src/Beacon.Connections/ProviderDsl.cs:29`.  
Protocol files: HTTP/webhook/GitHub REST, spawned command processes, and optional DSL YAML.  
Copy: action feedback is produced by `DetailWindow` and executor result messages; confirmation dialogs use `DetailWindow` copy.

## Native Shell and Presentation

The presentation layer receives state only through event subscription plus a snapshot/cache read. It never starts provider refresh itself. L0 chooses a shared pinned host or independent floating windows; L1 capsule/island summarizes aggregate state; L2 board combines cache, live events, aggregate sections, and notifications; L3 detail adds history and actions. A validated declarative tree can replace traditional rendering for tile/detail surfaces.

Implementation: `src/Beacon.App/App.xaml.cs:198`, `src/Beacon.App/Windows/PinnedHostWindow.cs:684`, `src/Beacon.App/Windows/PinnedHostWindow.cs:726`, `src/Beacon.App/Windows/FloatingTileWindow.cs:26`, `src/Beacon.App/Windows/CapsuleWindow.cs:23`, `src/Beacon.App/Windows/QuickPanelWindow.cs:40`, `src/Beacon.App/Windows/DetailWindow.cs:20`, `src/Beacon.App/Components/ComponentSurfaceRenderer.cs:35`, `src/Beacon.App/Infrastructure/NativeMethods.cs:6`, `src/Beacon.App/Services/TrayIconService.cs:13`  
Schema: declarative surface schema is `src/Beacon.Core/Models/ComponentDescriptor.cs:9`, validated at `src/Beacon.Core/Models/ComponentDescriptor.cs:34`; board geometry schema is `src/Beacon.Core/Models/QuickPanelLayout.cs:9`; shell file schema is private to `src/Beacon.App/Services/ShellStateStore.cs:10`.  
Protocol files: no UI protocol file. Win32, Toast activation, notification-icon, hotkey, and monitor P/Invoke are localized in `src/Beacon.App/Infrastructure/NativeMethods.cs` and each platform service.  
Copy: board/tray labels use `UiStrings`; `ComponentSurfaceRenderer` renders declarative text, and window-specific copy remains beside its window implementation.

## Why These Layers

1. **Core has no UI/provider/storage dependency.** Its project description says it contains models, abstractions, and UI/provider-free services (`src/Beacon.Core/Beacon.Core.csproj:7`), and the solution rules require no project references (`docs/solution-structure.md:44`). This lets scheduling, state, aggregation, notification evaluation, and action orchestration be tested without WinUI.
2. **Collection is isolated behind two provider interfaces.** `IConnectionProvider` tests connectivity while `IWidgetProvider` normalizes output (`src/Beacon.Core/Abstractions/IConnectionProvider.cs:12`, `src/Beacon.Core/Abstractions/IConnectionProvider.cs:29`). Startup registers 17 connection types and 20 widget types (`src/Beacon.App/Services/BeaconRuntime.cs:116`, `src/Beacon.App/Services/BeaconRuntime.cs:183`), so adding a source does not add a branch to refresh, aggregation, or display code.
3. **Persisted configuration, cache, secrets, notifications, and shell geometry are not one storage concept.** They have different recoverability and security rules: five recoverable configuration files (`src/Beacon.Storage/JsonConfigurationStore.cs:9`), best-effort cache (`src/Beacon.Storage/JsonCacheStore.cs:44`), DPAPI-only credentials (`src/Beacon.Storage/DpapiSecretStore.cs:9`), recoverable notification history (`src/Beacon.Storage/JsonNotificationRecordStore.cs:7`), and UI layout (`src/Beacon.App/Services/ShellStateStore.cs:6`). Collapsing them would couple data freshness, secrets, and user intent.
4. **Scheduling and normalization are separated from the source adapter.** `RefreshScheduler` owns grouping, jitter, and backoff (`src/Beacon.Core/Services/RefreshScheduler.cs:41`), while `WidgetHost` owns cache-first hydration, late-result protection, provider calls, health/stale publication, and debug override (`src/Beacon.Core/Services/WidgetHost.cs:93`, `src/Beacon.Core/Services/WidgetHost.cs:75`, `src/Beacon.App/Services/BeaconRuntime.cs:240`). A provider therefore cannot bypass cache, health, or invalidation semantics.
5. **State propagation is an in-process event contract.** `EventBus` is explicitly synchronous and thread-safe (`src/Beacon.Core/Services/EventBus.cs:6`), `StateStore` publishes only content changes (`src/Beacon.Core/Services/StateStore.cs:8`), and aggregation is a fold over that stream (`src/Beacon.Core/Services/StatusAggregator.cs:7`). This gives tray, capsule, L0, L2, and L3 one consistent snapshot source without making UI ask providers directly.
6. **Notifications split evaluation, output, and persistence.** Rule evaluation is Core (`src/Beacon.Core/Services/NotificationEngine.cs:8`), Toast output is App and implements `INotificationSink` (`src/Beacon.App/Services/ToastNotificationService.cs:20`), and records persist independently (`src/Beacon.Storage/JsonNotificationRecordStore.cs:12`). Runtime wiring connects all three (`src/Beacon.App/Services/BeaconRuntime.cs:148`, `src/Beacon.App/Services/BeaconRuntime.cs:154`), so platform Toast and durable history can evolve independently.
7. **Actions are confirmed centrally but executed by isolated adapters.** `ActionRunner` owns confirmation, timeout, retry, result events, and source refresh (`src/Beacon.Core/Services/ActionRunner.cs:8`, `src/Beacon.Core/Services/ActionRunner.cs:59`); `IActionExecutor` is one narrow operation contract (`src/Beacon.Core/Abstractions/IActionExecutor.cs:6`), and webhook requires a signed secret or refuses to send (`src/Beacon.Actions/WebhookExecutor.cs:61`, `src/Beacon.Actions/WebhookExecutor.cs:68`). Unsafe side effects do not bypass orchestration.
8. **Presentation consumes state and delegates all intent.** Board starts from cache, then subscribes to state, invalidation, aggregate, and notification events (`src/Beacon.App/Windows/QuickPanelWindow.cs:93`, `src/Beacon.App/Windows/QuickPanelWindow.cs:100`); pinned/floating hosts use the same state-event route (`src/Beacon.App/Windows/PinnedHostWindow.cs:726`, `src/Beacon.App/Windows/FloatingTileWindow.cs:74`); detail calls the runtime action runner (`src/Beacon.App/Windows/DetailWindow.cs:387`). Surfaces therefore stay renderable from snapshots and cannot become second polling engines.
9. **Declarative UI is a separate bounded renderer, not a web host.** `ComponentDescriptor` is pure data (`src/Beacon.Core/Models/ComponentDescriptor.cs:9`), validation has explicit depth/node/child limits (`src/Beacon.Core/Models/ComponentDescriptor.cs:31`, `src/Beacon.Core/Models/ComponentDescriptor.cs:36`), and rendering is a closed registry with diagnostic fallbacks (`src/Beacon.App/Components/ComponentSurfaceRenderer.cs:109`, `src/Beacon.App/Components/ComponentSurfaceRenderer.cs:201`). This preserves native rendering while allowing user-shaped tile/detail trees.
10. **App is the only composition root.** `BuildServices` registers process/platform services (`src/Beacon.App/App.xaml.cs:602`) and `BeaconRuntime.Start` wires stores, providers, scheduler, host, aggregator, notifications, actions, and update checking (`src/Beacon.App/Services/BeaconRuntime.cs:104`, `src/Beacon.App/Services/BeaconRuntime.cs:204`). Composition stays separate from reusable behavior, avoiding hidden static coupling between layers.
