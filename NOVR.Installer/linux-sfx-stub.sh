#!/bin/sh
set -eu

marker="__NOVR_PAYLOAD_BELOW__"
line=$(awk "/^$marker$/ { print NR + 1; exit 0; }" "$0")

if [ -z "$line" ]; then
  echo "Payload marker not found." >&2
  exit 1
fi

workdir=$(mktemp -d "${TMPDIR:-/tmp}/novr-installer.XXXXXX")
cleanup() { rm -rf "$workdir"; }
trap cleanup EXIT HUP INT TERM

payload="$workdir/payload.tar.gz"
tail -n +"$line" "$0" > "$payload"
tar -xzf "$payload" -C "$workdir"
chmod +x "$workdir/NOVR.Installer"
exec "$workdir/NOVR.Installer" "$@"
exit 1

__NOVR_PAYLOAD_BELOW__
