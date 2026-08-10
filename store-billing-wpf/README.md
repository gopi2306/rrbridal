# Store Billing (WPF) - RR Bridal

Windows WPF point-of-sale application with offline MongoDB support and a central-online operating mode.

## Automated tests

The solution contains two test projects:

- `RRBridal.StoreBilling.Tests`: domain, payload, service, and parity tests.
- `RRBridal.StoreBilling.UiTests`: Windows UI Automation tests using xUnit and FlaUI UIA3.

Run the non-UI tests:

```powershell
dotnet test .\src\RRBridal.StoreBilling.Tests\RRBridal.StoreBilling.Tests.csproj --configuration Release
```

Run the complete online and offline UI suite from an interactive Windows desktop. Offline tests require a local MongoDB service; every test uses and drops only a generated `rr_bridal_ui_<guid>` database:

```powershell
.\scripts\run-ui-tests.ps1 -Configuration Release -Mode Both
```

Run both suites:

```powershell
.\scripts\run-ui-tests.ps1 -Configuration Release -IncludeExistingTests -Mode Both
```

Run a targeted mode or category:

```powershell
.\scripts\run-ui-tests.ps1 -Mode Online
.\scripts\run-ui-tests.ps1 -Mode Offline -MongoUri "mongodb://127.0.0.1:27017"
.\scripts\run-ui-tests.ps1 -Mode Both -Category UiSmoke
.\scripts\run-ui-tests.ps1 -Mode Both -Category UiTransactional
```

UI tests launch an isolated stateful central-API fake and use a temporary application-data directory. They do not read or overwrite the signed-in Windows user's real till credentials, receipt settings, or billing settings. Printing, payment-terminal, and WhatsApp delivery are simulated and recorded under the temporary test data directory. Failure screenshots, UI trees, startup traces, and TRX results are written below `TestResults\ui` unless `RRBRIDAL_UI_ARTIFACTS` overrides the location.

UI Automation requires Windows and an interactive desktop. Run it from a logged-in self-hosted agent rather than a Windows service/session-0 runner. Physical printers, Pine Labs/Razorpay terminals, and WhatsApp delivery remain hardware/integration acceptance tests.

See [`docs/WPF_UI_AUTOMATION_REPORT.md`](docs/WPF_UI_AUTOMATION_REPORT.md) for scope, results, and remaining coverage.

