# 0022. `/manage` lives on the media port

- **Status:** Accepted
- **Date:** 2026-09-08
- **Formerly:** `docs/decisions.md` section 7b, standing decision 22

Reporting this decision as a defect is a known false positive - see the [index](README.md).

## Context

Decided 2026-09-08, during the fix pass for the third `/review-all --full` ([history.md](../history.md) section 6i). The compose file mandates `network_mode: host` because SSDP needs multicast, and `Program.cs` binds both ports with `ListenAnyIP` - so the media port is on every interface and there is nothing to publish or withhold. What made that dangerous was `ADMIN_HOSTNAME`: `HostFilteringOptions` is **process-wide**, so widening `AllowedHosts` for the admin pages widened it for `/manage` too, and the hostname is in the public Certificate Transparency logs.

## Decision

**`/manage` living on the MEDIA port is deliberate, and the compose overlay's "never publish the media port" advice was describing a control that does not exist.** `RejectRemoteManagementEndpointFilter` is the control now. Both documents were corrected.

## Consequences

**Do not re-raise the absence of authentication** - [0001](0001-no-authentication.md) still holds; this was about a specific documented setup contradicting itself. The bind-address alternative is [0023](0023-no-bind-address.md).
