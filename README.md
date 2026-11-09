# Background Sales Demo

This .NET 11 MAUI app stores individual sales in LiteDB and rebuilds daily,
product, and category summaries in SQLite. Dashboard reads the SQLite snapshot;
Sales writes to LiteDB and requests an immediate refresh. A background service
also refreshes every 15 seconds while the app is running. The two refresh paths
share a gate, and SQLite replaces all three summary tables in one transaction.
If SQLite is unavailable, a valid sale can still be saved to LiteDB; the status
reports that the summary refresh is pending.

The eight records in `Services/SalesSeedData.cs` match the supplied assignment
snippet; their timestamps are relative to the time of first seeding. Seeding
runs only when the `sales` collection is empty, so an existing database is
deliberately not overwritten.

The SDK is pinned in `global.json`; the assignment's LiteDB, SQLite, and MVVM
Toolkit package versions are pinned in the project. Open the solution in VS Code
with the .NET MAUI extension and select an available Debug target. The Debug
configuration includes the DevFlow agent for inspection. Both database
files are created under the app's `FileSystem.AppDataDirectory`. The
constructor-injected Shell and pages are resolved when the window is created,
after application resources have been initialized.
The Dashboard uses a flattened, virtualized list with separate typed templates
for chart headings and rows; this avoids the Windows grouped-list handler
binding a section to an item template. Mac Catalyst's Debug-only entitlements
allow the DevFlow listener, while Release keeps the default sandbox permissions.

The timer is an in-process service, not an operating-system background job;
mobile platforms may suspend it while the app is not active. The app requests
service cancellation when its window is destroyed and restarts it if the
window is recreated; forced process termination may bypass lifecycle events.
Monetary calculations use `decimal` before the assignment's SQLite `REAL` storage, so
raw SQLite sums can show floating-point rounding beyond currency precision.

Run the focused data-pipeline tests with
`dotnet test .\Tests\SalesDemo.Tests.csproj`. They use temporary databases and
include a real 15-second timer test; they do not replace device verification.
The local `submission` folder contains the captured Windows run, database
inspection evidence, and answers to the supplied reflection questions. It is
kept separate from the source repository and solution archive.
