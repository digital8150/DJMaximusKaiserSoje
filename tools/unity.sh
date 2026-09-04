#!/usr/bin/env bash
# Runs a Unity editor method in batch mode and reports whether it succeeded.
# Usage: tools/unity.sh <FullyQualified.Method> [log-name]
set -u

UNITY="/c/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe"
PROJECT="F:\\repos\\DJMaximusKaiserSoje"
METHOD="$1"
NAME="${2:-$(echo "$METHOD" | tr '.' '-')}"
# Unity refuses a log path under a dot-prefixed directory, so build output lives here instead.
LOG="artifacts/logs/unity-${NAME}.log"

mkdir -p artifacts/logs
rm -f "$LOG"

"$UNITY" -batchmode -quit \
  -projectPath "$PROJECT" \
  -executeMethod "$METHOD" \
  -logFile "$LOG"

STATUS=$?
echo "unity exit=$STATUS  log=$LOG"
grep -nE "error CS|Exception|Aborting|executeMethod|Compilation failed" "$LOG" | head -40
exit $STATUS
