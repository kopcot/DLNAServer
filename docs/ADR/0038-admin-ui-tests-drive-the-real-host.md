# 0038. Admin UI tests drive the real host with Selenium, Linux CI only

- **Status:** Accepted
- **Date:** 2026-11-10

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Nothing tested the admin pages as a browser sees them: the integration tests call controllers and helpers directly, and no host is booted. [decisions.md](../decisions.md) section 7 lifted the "no `WebApplicationFactory`" rule for a small set of host-level tests, and section 7b named why none had been written - an in-memory TestServer leaves `Connection.LocalPort` at 0, which `AdminOrMediaPortEndpointFilter` and `AdminSurfaceMiddleware` read, and `Program.Main` sets the process-wide working directory and owns the restart loop. An admin CSS reskin was next, and it needed something that fails when a page stops working rather than when it changes colour.

## Decision

**`tests/DlnaServer.UiTests` drives the admin pages with Selenium.WebDriver and headless Chrome, against the real Host started as its own process from its own build output.** The Host gets two free ports, a temporary `config.json` (through `DLNA_CONFIG_FILE`), database (through `ConnectionStrings__DlnaDatabase`) and media tree; when the run ends it is stopped through `/manage/stop` - killed only if that fails - and its folder deleted. Real Kestrel on real ports and the real `Main` sidestep both 7b obstacles with no change under `src/`. One Host and one browser serve the whole run; destructive actions are armed and cancelled, never confirmed.

**They run on Linux only in CI.** `dotnet.yml`'s Windows leg filters them out with `FullyQualifiedName!~DlnaServer.UiTests`: that leg exists for path handling, which these tests do not exercise, so running them twice buys nothing but a second browser to keep working. Decided by the maintainer on 2026-11-10.

## Consequences

- The tests select by tag, id, link address and visible text, never by CSS class, so a restyle that keeps the pages working keeps them green.
- A local run needs Chrome; Selenium Manager fetches the driver, with its usage statistics switched off (`SE_AVOID_STATS`). On a GitHub runner the image's own chromedriver is used through `CHROMEWEBDRIVER`, so CI downloads nothing at test time. While it runs, the test Host advertises itself over SSDP as *ZEN DLNA Server (UI tests)* - there is no switch to stop that without a change under `src/` - and writes its logs to the Host's own `bin` folder.
- The test Host runs as `Development`, because that is the only environment that serves the stylesheet from the build folder, and it listens on every interface like the product does. So the stylesheet test proves the Development static-asset path, not the published one - the smoke test covers that - and during a local run the developer exception page is reachable from the LAN.
- A page that throws in its interactive render ends its Blazor circuit without reaching the layout's error boundary, which is static. The tests read the browser console and fail on any error in it, which is how such a page is caught.
- **`release.yml` runs the UI tests too, on purpose.** It tests the whole solution, because a release must not ship what the build gate would have rejected. A UI test that fails there leaves the tag without a release; re-run the failed job, as the [workflows README](../../.github/workflows/README.md) describes. A release run keeps no test results, so the failed page's screenshot is only in `dotnet.yml`'s `test-results-<os>` artifact. Both build jobs stop after 30 minutes, so a hung browser cannot hold a runner for hours.
- Coverage collected from this project says nothing about the Host: the Host runs in another process, where the collector does not reach.
- Running a UI test on Windows CI is not a gap; it is filtered out on purpose.
- Host-level HTTP tests through `WebApplicationFactory` remain unwritten; this covers the admin pages only.
