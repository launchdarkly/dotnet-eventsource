#!/usr/bin/env bash

# Starts the contract-test service in a connected Android emulator.
#
# This script assumes adb is on PATH and an emulator is already running. It installs the
# APK, launches the MainActivity, waits for the app process to appear, forwards the local
# port to the emulator, and exits. Modeled on android-client-sdk/scripts/start-test-service.sh.
#
# Optional environment variables:
#   LOCAL_PORT: host-side port that will forward into the emulator (default 8000)
#   ANDROID_PORT: emulator port for adb targeting (default 5554)
#   APK_PATH: path to the debug APK to install
#   APP_ID: Android application ID (must match the ApplicationId in the .csproj)

set -eo pipefail

LOCAL_PORT=${LOCAL_PORT:-8000}
ANDROID_PORT=${ANDROID_PORT:-5554}
SERIAL_NUMBER=emulator-${ANDROID_PORT}
APP_ID=${APP_ID:-com.launchdarkly.contracttestservice}
APK_PATH=${APK_PATH:-contract-tests-android/bin/Debug/net9.0-android/${APP_ID}-Signed.apk}

if [ ! -f "$APK_PATH" ]; then
  # Fall back to the unsigned APK if the signed one isn't present -- .NET for Android emits
  # -Signed.apk under some configurations and a bare .apk under others.
  APK_PATH=$(find contract-tests-android/bin/Debug/net9.0-android -name '*.apk' -not -name '*Signed*' | head -1)
  if [ -z "$APK_PATH" ] || [ ! -f "$APK_PATH" ]; then
    echo "No APK found under contract-tests-android/bin/Debug/net9.0-android/" >&2
    exit 1
  fi
fi

echo "Installing $APK_PATH to $SERIAL_NUMBER"
adb -s "$SERIAL_NUMBER" install -t -r -d "$APK_PATH"

# The MainActivity's fully qualified name includes a compiler-generated crc64 namespace
# hash, so we discover it via dumpsys rather than hardcoding.
ACTIVITY=$(adb -s "$SERIAL_NUMBER" shell dumpsys package "$APP_ID" | grep -oE "${APP_ID}/[a-zA-Z0-9._]+MainActivity" | head -1)
if [ -z "$ACTIVITY" ]; then
  echo "Could not find MainActivity for $APP_ID via dumpsys" >&2
  exit 1
fi
echo "Launching $ACTIVITY"
adb -s "$SERIAL_NUMBER" shell am start -n "$ACTIVITY"

# Wait for the app process to be running before setting up port forwarding.
APP_PID=""
TIMEFORMAT='App started in %R seconds'
time {
  for _ in $(seq 1 30); do
    APP_PID=$(adb -s "$SERIAL_NUMBER" shell pidof -s "$APP_ID" 2>/dev/null || true)
    if [ -n "$APP_PID" ]; then break; fi
    sleep 1
  done
}
if [ -z "$APP_PID" ]; then
  echo "App process $APP_ID did not appear within 30 seconds" >&2
  exit 1
fi

adb -s "$SERIAL_NUMBER" forward "tcp:$LOCAL_PORT" "tcp:$LOCAL_PORT"

# Wait for the HTTP server inside the app to bind port 8000.
for _ in $(seq 1 30); do
  if curl -sS "http://localhost:$LOCAL_PORT/" >/dev/null 2>&1; then
    echo "Test service listening on localhost:$LOCAL_PORT (forwarded to emulator)"
    exit 0
  fi
  sleep 1
done
echo "Test service did not respond on localhost:$LOCAL_PORT after app launch" >&2
exit 1
