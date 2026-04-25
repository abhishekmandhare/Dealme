#!/bin/sh
# Start Xvfb in the background and hand off to node.
# Using an entrypoint script instead of xvfb-run because xvfb-run buffers
# stdout and doesn't exit cleanly when the child dies — both bad for a
# long-lived Docker service.

set -e

DISPLAY_NUM="${DISPLAY_NUM:-99}"
SCREEN="${XVFB_SCREEN:-1920x1080x24}"

Xvfb ":${DISPLAY_NUM}" -screen 0 "${SCREEN}" -nolisten tcp &
XVFB_PID=$!

trap 'kill -TERM "$XVFB_PID" 2>/dev/null || true; kill -TERM "$X11VNC_PID" 2>/dev/null || true' EXIT INT TERM

export DISPLAY=":${DISPLAY_NUM}"

# Chromium writes Singleton* files on launch and removes them on clean exit.
# If the container was SIGKILLed last time, these get orphaned and block the
# next launch with "profile appears to be in use". Safe to clear on startup.
PROFILE_DIR="${PROFILE_DIR:-/data/browser-profile}"
rm -f "${PROFILE_DIR}"/Singleton* 2>/dev/null || true

# Give Xvfb a moment to come up
sleep 0.5

# Expose the virtual display over VNC (port 5900) so the user can log in.
# -nopw = no password (local dev only), -forever = don't exit after one client.
x11vnc -display ":${DISPLAY_NUM}" -nopw -forever -shared -quiet &
X11VNC_PID=$!

exec node "$@"
