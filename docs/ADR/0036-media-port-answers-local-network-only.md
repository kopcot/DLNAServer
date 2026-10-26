# 0036. The media port answers the local network only

- **Status:** Accepted
- **Date:** 2026-10-26

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

[0029](0029-admin-port-answers-local-network-only.md) confined the admin port to the local network and left the media port open to any peer, and `PortEndpointFilterTest` pinned that openness on purpose. The `/review-all --full` pass of 2026-10-26 (finding M-1) showed what that left: from a global IPv6 peer, `/manage` answered 404 while `/fileserver/file/<id>` answered 200 with the whole file and a SOAP Browse listed every item. Kestrel binds both ports with `ListenAnyIP`, which includes IPv6, so wherever the router lets IPv6 in - host networking, a QNAP with no host firewall - the whole library could be listed and downloaded from the internet. [0022](0022-manage-on-the-media-port.md) and [0023](0023-no-bind-address.md) explain the binding; neither covered the rest of the media port.

## Decision

**The media port answers the local network only, by the same check as the admin port.** `AdminSurfaceMiddleware` refuses any request, on either port, whose socket peer is not local per `LocalNetworkAddress` - loopback, the private and link-local ranges, IPv6 unique-local, CGNAT (for Tailscale), and a global IPv6 peer in the server's own /64 - with the same 404 `/manage` gives. The peer is the one recorded before `UseForwardedHeaders`, for every request now rather than only the admin port's. The middleware sits ahead of static files, the SOAP endpoints and every controller, so Browse, `/fileserver`, thumbnails, subtitles, GENA, SCPD, icons and `description.xml` are all covered, and so is any route added later. Refusals are logged at Debug only, because anything on the internet can send them at any rate. Decided by the maintainer on 2026-10-26, choosing "gate to local network" over leaving the port open.

## Consequences

- No television on the LAN is refused: SSDP advertises an IPv4 address only, and a renderer on the same network connects from a private one.
- A renderer reaching the server from elsewhere - a routed VPN with public addresses, a public IPv6 address on another network - now gets 404. That is the decision, not a regression.
- A LAN that numbers its devices from **public IPv4** space is refused too: the IPv4 rule is a list of ranges, with no same-subnet test like the IPv6 /64 one. Accepted rather than solved; no deployment is known to do this.
- The check judges the socket's peer, so a NAT or proxy in front that rewrites the source to a private address - a Docker bridge's port proxy, a router's source NAT - makes it pass everything. The shipped deployments use host networking, where it holds; keep the host firewall either way.
- `100.64.0.0/10` counts as local, for Tailscale, as it already did for the admin port. Where the ISP itself uses that range for carrier-grade NAT, another of its subscribers may be able to reach the library - see the CGNAT note in [operations.md](../operations.md).
- `RejectRemoteManagementEndpointFilter` stays. It now runs behind this check and judges the forwarded address rather than the socket's, so it is the stricter of the two for `/manage`.
- [0001](0001-no-authentication.md) still stands. This is an address check, not authentication, and keeping the host firewall is still advised.
