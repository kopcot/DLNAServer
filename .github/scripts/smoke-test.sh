#!/usr/bin/env bash
# Smoke test a running server over HTTP - the CI workflows start it, this only asks it questions.
#
# A runner is not a LAN with a television on it: SSDP multicast has nobody to answer, so discovery is not
# tested here. What is tested is everything a television and an operator reach over HTTP, on the default
# ports from config.json.
set -euo pipefail

media="http://127.0.0.1:26852"
admin="http://127.0.0.1:26853"

# Readiness, not liveness - a fresh database has to have migrated and the configuration to have validated.
for _ in $(seq 1 60); do
  if curl --fail --silent "${media}/health/ready" > /dev/null \
    && curl --fail --silent "${admin}/admin/health" > /dev/null; then
    break
  fi
  sleep 1
done
curl --fail --silent --show-error "${media}/health/ready"
echo

# description.xml is the first thing a television fetches after discovery.
curl --fail --silent --show-error "${media}/" | grep --quiet "urn:schemas-upnp-org:device:MediaServer:1"
echo "description.xml advertises a MediaServer"

curl --fail --silent --show-error "${admin}/admin" | grep --quiet "_framework/blazor"
echo "admin UI renders on the admin port"

# The two-port split is the admin UI's only protection from a renderer on the LAN.
status=$(curl --silent --output /dev/null --write-out "%{http_code}" "${media}/admin")
if [ "${status}" != "404" ]; then
  echo "admin UI answered ${status} on the media port, expected 404" >&2
  exit 1
fi
echo "admin UI is not reachable on the media port"
