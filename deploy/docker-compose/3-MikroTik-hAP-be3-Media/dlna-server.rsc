# DLNA server as a RouterOS container on the MikroTik hAP be3 Media. Read README.md in this folder first.
#
# Written against MikroTik's Container documentation as of 2026-10-01. NOT YET RUN ON THE DEVICE.
#
# Assumes the RouterOS defaults - LAN bridge named "bridge", LAN 192.168.88.0/24, router at .1 - plus
# container mode enabled and an ext4 USB disk named usb1 with your media in usb1/media. Change those
# below to match /disk print and /ip address print, then:
#
#   /import dlna-server.rsc

# The container gets its own address ON the LAN bridge, outside the default DHCP pool (.10-.254). That is
# what lets SSDP multicast reach televisions: no NAT, no port forwarding, and the server advertises an
# address a television can actually route to.
/interface/veth/add name=veth-dlna address=192.168.88.2/24 gateway=192.168.88.1
/interface/bridge/port/add bridge=bridge interface=veth-dlna

# Pull from GHCR, and extract on the USB disk - the 512 MB internal NAND cannot hold the image.
# memory-high caps containers at 768 MiB of the router's 2 GB; below ~512 MiB thumbnailing a large
# photo fails rather than being slow.
/container/config/set registry-url=https://ghcr.io tmpdir=usb1/tmp memory-high=805306368

# Media read-write: previews are written beside the media, in .@__thumb folders.
/container/mounts/add list=dlna src=usb1/media dst=/media
/container/mounts/add list=dlna src=usb1/dlna/data dst=/data
/container/mounts/add list=dlna src=usb1/dlna/logs dst=/app/logs

# Index 0 replaces the folder the shipped config.json names rather than adding to it.
/container/envs/add list=dlna key=Dlna__Library__SourceFolders__0 value=/media
/container/envs/add list=dlna key=Dlna__Server__FriendlyName value="ZEN DLNA Server (MikroTik)"
# Files copied onto the disk over SMB or by unplugging it are not reliably seen by the file watcher.
/container/envs/add list=dlna key=Dlna__Library__UsePeriodicRescan value=true
/container/envs/add list=dlna key=Dlna__Library__RescanIntervalMinutes value=15
# ffmpeg is in the image; on would download and execute an unverified binary.
/container/envs/add list=dlna key=Dlna__Thumbnails__DownloadFFmpeg value=false
# The memory settings docker-compose.yml in folder 1 ships, for the same reason - they decide the footprint.
/container/envs/add list=dlna key=Dlna__Database__MemoryMapLimitInMegabytes value=0
/container/envs/add list=dlna key=Dlna__Database__CacheSizeInMegabytes value=2
/container/envs/add list=dlna key=Dlna__FileCache__Enabled value=false
/container/envs/add list=dlna key=TZ value=Europe/Prague

# user=0:0 because a disk RouterOS formats is owned by root and RouterOS has no chown, so the image's own
# non-root user could not write previews into usb1/media or the database into usb1/dlna/data.
/container/add name=dlna-server hostname=dlna-server remote-image=kopcot/dlnaserver:latest \
    interface=veth-dlna root-dir=usb1/containers/dlna mountlists=dlna envlist=dlna \
    user=0:0 start-on-boot=yes logging=yes
