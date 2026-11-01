# API usability improvements

Updated on 2026-11-01 (Asia/Seoul).

Repository layout follow-up: the package regression checks now live in `samples/Calendar.Example/MainWindow.PackageChecks.cs` behind `EnablePackageChecks=true`. The same example consumes NuGet packages for both demonstration and validation; there is no separate consumer project. See [development](development.md) for current build commands. Earlier artifact paths below describe the runs before this reorganization.

## Changes and migration

| Scenario | API and behavior |
| --- | --- |
| Select an application object | Assign `SelectedItem`. Read `SelectedOccurrence`, or bind the existing `SelectedEvent` dependency property. Both selection properties stay synchronized. |
| Preserve selection after edits or replacement | Source reference identity survives data edits. Optional `ICalendarEventKeyProvider` keys preserve logical selection across replacement objects. Keys must be stable and unique per occurrence. |
| Load events for the initial range | Subscribe to `VisibleRangeChanged` before load. The initial notification has a null `OldRange`; later notifications report changes to the loaded range. Pre-load property changes do not emit intermediate ranges. |
| Adapt application models | Use `CalendarEventAdapter.Create<T>(data, text, key)` or the existing adapter interface. Source, adapter, and pending selection can be assigned together during synchronous initialization. |
| Customize event containers | Use `EventContainerStyle` and `EventContainerStyleSelector`. Target `ContentControl` to support interactive and display-only events. Additional setters retain the built-in template unless explicitly replaced. |
| Configure event input | `IsEventSelectionEnabled` controls retained selection. `IsEventInvocationEnabled` controls `EventInvoked`. Both default to true. Independent template inputs remain enabled when parent actions are disabled. |
| Apply bulk changes | Use nested, UI-thread `DeferRefresh()` scopes. Automatic changes are coalesced; unchanged models reuse projections and change subscriptions. Explicit `RefreshEvents()` forces full model re-evaluation outside a scope. |
| Set application minimum size | Rendering preserves `MinWidth`, `MinHeight`, and their bindings. The default minimum width is a style setter; internal row minimums reserve header and event space. |
| Work with calendar dates | C# helpers `DisplayDateOnly`, `SelectedDateOnly`, and `Today`, plus interaction `DateOnly`, avoid date-transport conversions. Existing dependency properties remain available for XAML bindings. |

`SelectedEventChanged` reports a logical selection identity change. Date, title, time-zone, or same-key object replacement does not count as a new selection. `SelectedItemChanged` reports replacement of the original application object, even if its key is unchanged. A requested item can remain pending until data arrives; removing a previously resolved selection clears it.

To retain the old display-only event policy, set **both** `IsEventSelectionEnabled` and `IsEventInvocationEnabled` to false. Disabling selection alone now permits event invocation. Template buttons remain independently usable; disable those explicitly in application templates if desired.

WinUI does not preserve data bindings placed in style setter values. The implementation uses native styles and a per-event style selector instead of claiming support for that syntax. Ordinary bindings in content templates continue to work. Avoid keeping a refresh scope across `await`: fetch first, then apply data inside the scope.

## Validation

- Solution build and documentation/library build: zero warnings and errors.
- Core tests: 29 cases each on .NET 8, 9, and 10.
- Fresh isolated NuGet cache, with no ProjectReference in the consumer: .NET 8.0.33, 9.0.22, and 10.0.14 passed eight API regression groups each, plus date, event, and overflow activation. Results: `artifacts/package-smoke-20261101-183028/results.json`.
- The native consumer checks initial range delivery, 5/6-week transitions, source-before-adapter initialization, pending selection before adapter replacement, stable-key selection, removal, edit preservation, legacy selection synchronization, batch refresh, incremental notifications, nested/idempotent scopes, date/time-zone helpers, minimum size preservation, XAML and selected container styles, and all event input combinations including nested buttons.
- The 1,000-item batch adapts each item once. A single model update or addition adapts only that model; reordering unchanged models invokes no adapter. These are operation-count assertions, not frame-rate benchmarks.
- Documentation build: 49 HTML pages, seven namespaces, 541 local link/anchor checks; generated API summaries, source links, project-site paths, and English-only output passed.
- Local example UI: 23 interaction scenarios passed, including the independent event invocation toggle, in `artifacts/api-usability-ui-final-20261101/results.json`. Resize regression: seven scenarios passed in `artifacts/api-usability-resize-20261101/results.json`.
- Language/time-zone regression: six scenarios passed in `artifacts/api-usability-locale-verified-20261101/results.json`. The example now replaces translated system-choice items to invalidate ComboBox's selected-content cache, and the harness targets the main HWND when sending keys to an owned popup.

Windows Sandbox startup failed before app deployment with `sandbox_start_failed` / `0x80070005` (`E_ACCESSDENIED`). Native consumer validation ran locally. This report does not claim a Sandbox pass, Native AOT validation, or new Outlook/FPS performance measurements.
