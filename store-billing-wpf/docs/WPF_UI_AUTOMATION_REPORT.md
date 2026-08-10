# WPF Functional Automation Report

Date: 7 August 2026  
Application: RR Bridal Store Billing (`net9.0-windows`)  
Automation: xUnit + FlaUI UIA3 + stateful loopback central fake + isolated local MongoDB  
Execution environment: Windows interactive desktop, Release application build

## Executive result

The complete automated run passed.

| Verification | Result |
| --- | ---: |
| WPF Release build | Passed, 0 errors |
| Existing domain/service tests | 114 passed, 0 failed |
| Functional UI project | 31 passed, 0 failed, 0 skipped |
| Executable FlaUI scenarios | 21 |
| Stateful fixture contracts | 10 |
| Online executable scenarios | 14 |
| Offline executable scenarios | 7 |
| Total automated tests | 145 passed |
| Explicit WPF AutomationIds | 273 |

The final full UI project completed in 4 minutes 8 seconds. Six transactional tests were also executed twice more; both stability runs passed 6/6 in approximately 1 minute 34 seconds each.

## Functional coverage

### P0: revenue and day operations

- Central-online and offline Mongo login, including invalid credentials.
- Shell status, seeded open-day visibility, logout and relogin.
- All 18 configured shell routes with visibility-aware assertions.
- Hold, resume, and cash bill posting through the actual WPF dialogs.
- Online bill persistence asserted against stateful fake-central state and requests.
- Offline bill persistence asserted in `store_bills`, with pending `outbox_events` and notification visibility.
- Posting without an open day is blocked and produces no bill write.
- Online/offline request and persistence parity checks.

### P1: business operations

- Daily-expense invalid-draft validation in both modes, including no-write assertions.
- Offline seeded customer and salesman usability.
- Online customer, salesman, and quotation page actions and result states.
- Sale-return source switching and legacy-invoice validation fields.
- Stateful expense create/update/list contracts.
- Stateful bill, held-bill, return, quotation, credit-note, payment, and adjustment lifecycle contracts.

### P2: administration and reporting

- Credit bills, online COD sales, and dashboard empty-state/action behavior.
- Bill-lookup validation and cancel behavior.
- Analytics, ledger, and barcode page behavior, including barcode print validation.
- Settings access and section visibility.
- Dashboard, inventory, master-data, reporting, lookup, numbering, and day-session fake contracts.

The reusable page-object layer also covers Billing, Payment, bill confirmation, held bills, Customers, Salesmen, Quotations, Returns, Credit Bills, Online Sales, Daily Expenses, Inventory/Dashboard, Bill Lookup, Day Close/Cash Handover, Settings, Notifications, print previews, and common dialogs.

## Dual-mode isolation

Every executable test uses:

- a fresh loopback central fake with scenario-owned state and request history;
- a fresh application process and temporary working directory;
- a unique `RRBRIDAL_DATA_DIR`;
- deterministic store, device, counter, user, product, customer, salesman, and day-session fixtures;
- simulation mode enabled only through `RRBRIDAL_UI_AUTOMATION`.

Online mode does not require MongoDB. Offline mode connects to the configured local Mongo service and creates a database named exactly `rr_bridal_ui_<32-hex-guid>`. Cleanup refuses any database name outside that strict pattern and drops only the generated test database.

No production central endpoint, database, credentials, or per-user till settings are used.
After the final smoke verification, a Mongo database-list probe confirmed that zero `rr_bridal_ui_*` databases remained.

## Simulated integrations

When `RRBRIDAL_UI_AUTOMATION=1`:

- thermal, office, and barcode print jobs are recorded as deterministic JSON under `RRBRIDAL_DATA_DIR` instead of reaching the Windows spooler;
- Razorpay/POS calls return deterministic successful simulation records;
- WhatsApp settings, sends, tests, and broadcasts are recorded without external delivery.

When the variable is absent, production behavior remains unchanged.

## Diagnostics and repeatability

- UI tests are serialized to protect desktop focus.
- All waits are bounded and visibility-aware; collapsed pages do not satisfy route assertions.
- Each test resets fake state, Mongo state, application data, and the child process.
- Failures capture screenshots, UI Automation trees, startup traces, fake request summaries, and TRX output.
- The runner supports `-Mode`, `-Category`, `-MongoUri`, and `-Repeat`.

## Reproduction

From `store-billing-wpf` in an unlocked interactive Windows session:

```powershell
.\scripts\run-ui-tests.ps1 -Configuration Release -IncludeExistingTests -Mode Both
```

Run targeted or repeated suites:

```powershell
.\scripts\run-ui-tests.ps1 -Mode Online
.\scripts\run-ui-tests.ps1 -Mode Offline -MongoUri "mongodb://127.0.0.1:27017"
.\scripts\run-ui-tests.ps1 -Mode Both -Category UiSmoke
.\scripts\run-ui-tests.ps1 -Mode Both -Category UiTransactional -Repeat 2
```

Results and failure artifacts are written under `TestResults/ui` with timestamped TRX names.

## Deterministic-suite boundary

The suite validates real WPF behavior while intentionally simulating external devices and delivery. It does not certify physical printer alignment, a real Pine Labs/Razorpay terminal, live WhatsApp delivery, production Mongo/central infrastructure, or multi-PC ZeroTier failover. Those remain manual hardware/environment acceptance checks.

## CI recommendation

Run the 114 non-UI tests on every change. Run `UiSmoke` on every change from a logged-in interactive Windows self-hosted agent. Run the complete online/offline suite nightly or before release and publish `TestResults/ui` on every failure.
