# 3 - MikroTik hAP be3 Media

The server on the [hAP be3 Media](https://mikrotik.com/product/hap_be3_media) router, with the media on a
USB disk: an IPQ-5322 (ARM64, 4 cores), 2 GB of RAM, 512 MB of NAND and 3 USB 3.0 ports, running
RouterOS 7 with the `container` package.

> **Not yet run on the device.** `dlna-server.rsc` is written against MikroTik's
> [Container documentation](https://help.mikrotik.com/docs/display/ROS/Container) as of 2026-10-01. The
> image itself is verified on ARM64; the RouterOS side is not. Expect to adjust names and addresses.

**There is no docker-compose here.** RouterOS runs containers but not compose, and it cannot build an
image. It pulls the published multi-arch `ghcr.io/kopcot/dlnaserver` image, which releases build from
this repository for `linux/amd64` and `linux/arm64`.

## One-time preparation

1. **Enable containers.** This needs physical access: run the command, then press the router's button
   when asked.
   ```
   /system/device-mode/update container=yes
   ```
2. **Install the `container` package** for your RouterOS version and reboot, if it is not already there.
3. **Prepare the USB disk.** Find its name with `/disk print`; it may be `usb1` or `usb1-part1`. Format it
   ext4 from RouterOS, then copy your media into a `media` folder on it. The script assumes `usb1`, so
   replace that everywhere if yours differs.
   ```
   /disk/format-drive usb1 file-system=ext4
   ```
   Formatting erases the disk. The image, its extraction space, the database and the previews all live on
   this disk, because the 512 MB of internal NAND cannot hold them. MikroTik recommends a disk with at
   least 100 MB/s sequential throughput.
4. **Check the image is public.** The script pulls `ghcr.io/kopcot/dlnaserver:latest` anonymously. A GHCR
   package is private until made public; for a private one, set `username` and `password` (a personal
   access token with `read:packages`) under `/container/config`.

## Install

Edit the addresses at the top of `dlna-server.rsc` to match your LAN (`/ip address print`), upload it to
the router, then:

```
/import dlna-server.rsc
/container/print
```

Wait until the container's status is `stopped` (pulled and extracted), then start it:

```
/container/start dlna-server
```

Admin pages: `http://192.168.88.2:26853`. Televisions find the server on their own. The first scan of a
large library takes a while on this CPU.

## What the script does, and why

| Setting | Why |
| --- | --- |
| veth on the LAN bridge, own address | SSDP is multicast. A container on a separate subnet behind NAT would be invisible to every television. On the LAN bridge, the server is a LAN host like any other |
| `registry-url=https://ghcr.io` | The published image lives on GHCR, not Docker Hub |
| `tmpdir` and `root-dir` on USB | The image does not fit in 512 MB of NAND |
| `memory-high` 768 MiB | Leaves the router its own memory; below ~512 MiB, thumbnailing a large photo fails |
| `user=0:0` | A disk RouterOS formats is root-owned and RouterOS has no chown, and previews are written beside the media |
| Periodic rescan every 15 min | Files copied over SMB or by moving the disk are not reliably seen by the file watcher |

## Updating

Stop and remove the container, then re-run the `/container/add` line from the script. The new pull gets
whatever `latest` is now. Mounts and env lists are kept, and so are the database and previews, which live
on the USB disk.

```
/container/stop dlna-server
/container/remove dlna-server
```

## If televisions do not see it

- **IGMP snooping** on `bridge` is the most likely cause: it can drop the SSDP multicast the server sends
  to `239.255.255.250`. Check with `/interface/bridge print`, and try turning snooping off.
- Check that the server advertises the veth address (`192.168.88.2`) and nothing else:
  `/log print where topics~"container"`.
- [`Docker.usage.md`](../../../Docker.usage.md) and [troubleshooting](../../../docs/troubleshooting.md)
  cover the server side.
