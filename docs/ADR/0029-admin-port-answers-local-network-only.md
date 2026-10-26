# 0029. The admin port answers the local network only

- **Status:** Accepted
- **Date:** 2026-09-26
- **Formerly:** `docs/decisions.md` section 7b, standing decision 29

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

[history.md](../history.md) section 6u.

## Decision

**The admin port answers the local network only, and that is not authentication.** `AdminSurfaceMiddleware` refuses an admin-port request whose socket peer is not local per `LocalNetworkAddress` - private and link-local ranges, loopback, CGNAT (for Tailscale), and a global IPv6 peer in the server's own /64. The peer is recorded **before** `UseForwardedHeaders` rewrites it, which is what keeps the Caddy overlay working without trusting a client-sent header.

## Consequences

[0001](0001-no-authentication.md) still stands; do not read this as a step towards auth, and do not "simplify" it to read `RemoteIpAddress` after the forwarder - that locks out the proxy's users or, the other way round, admits a forged header.
