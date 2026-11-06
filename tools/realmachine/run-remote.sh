#!/usr/bin/env bash
# Run a PowerShell script (or a directory of them) on a remote Windows box inside its
# INTERACTIVE console session (schtasks /IT), wait for the ###DONE marker appended by a
# wrapper .cmd, print the log, then pull requested files back into ./evidence-out/.
#
# usage: run-remote.sh <script.ps1 | dir/> [host] [user] [wait_sec] [fetch1,fetch2,...]
#   fetch items ending in "/" are pulled recursively (scp -r).
set -u
SRC="$1"; HOST="${2:-10.240.0.5}"; USER="${3:-admin}"; WAIT="${4:-900}"; FETCH="${5:-}"
SSH=(-o StrictHostKeyChecking=no -o BatchMode=yes -o ConnectTimeout=15)
REMOTE_DIR='C:\beacon-verify'
LOG='C:\beacon-verify\out.txt'

ssh "${SSH[@]}" "$USER@$HOST" 'if not exist C:\beacon-verify mkdir C:\beacon-verify' >/dev/null

if [ -d "$SRC" ]; then
  BASE="$(basename "$SRC")"
  ENTRY=""
  for cand in run.ps1 verify.ps1; do [ -f "$SRC/$cand" ] && ENTRY="$cand"; done
  [ -n "$ENTRY" ] || ENTRY="$(basename "$(ls "$SRC"/*.ps1 | grep -v '/lib\.ps1$' | head -1)")"
  REMOTE_SCRIPT="$REMOTE_DIR\\$BASE\\$ENTRY"
  # copy CONTENTS into the target dir (a bare `scp -r dir dest` nests dir/ dir on repeat runs)
  ssh "${SSH[@]}" "$USER@$HOST" "if not exist $REMOTE_DIR\\$BASE mkdir $REMOTE_DIR\\$BASE" >/dev/null
  scp -q -r "${SSH[@]}" "$SRC"/* "$USER@$HOST:C:/beacon-verify/$BASE/" || { echo "scp dir failed"; exit 1; }
else
  LEAF="$(basename "$SRC")"
  REMOTE_SCRIPT="$REMOTE_DIR\\$LEAF"
  scp -q "${SSH[@]}" "$SRC" "$USER@$HOST:C:/beacon-verify/$LEAF" || { echo "scp script failed"; exit 1; }
fi

# wrapper .cmd: run the script, capture output, append ###DONE exit=<code>
# (space before %ERRORLEVEL% is load-bearing: `0>>` parses as a file-handle redirect in cmd)
cat > /tmp/beacon-run.cmd <<EOF
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "$REMOTE_SCRIPT" > "$LOG" 2>&1
echo ###DONE exit= %ERRORLEVEL% >> "$LOG"
EOF
scp -q "${SSH[@]}" /tmp/beacon-run.cmd "$USER@$HOST:C:/beacon-verify/run.cmd" || { echo "scp wrapper failed"; exit 1; }

ssh "${SSH[@]}" "$USER@$HOST" 'schtasks /delete /tn BeaconVerify /f >nul 2>&1' >/dev/null
ssh "${SSH[@]}" "$USER@$HOST" 'schtasks /create /f /tn BeaconVerify /sc once /st 23:59 /tr "C:\beacon-verify\run.cmd" /it' || exit 1
ssh "${SSH[@]}" "$USER@$HOST" 'schtasks /run /tn BeaconVerify' || exit 1

deadline=$(( $(date +%s) + WAIT ))
while :; do
  sleep 6
  content="$(ssh "${SSH[@]}" "$USER@$HOST" 'type C:\beacon-verify\out.txt 2>nul' 2>/dev/null)"
  case "$content" in *'###DONE exit='*) break;; esac
  if [ "$(date +%s)" -ge "$deadline" ]; then echo "TIMEOUT waiting for remote script"; echo "$content"; exit 2; fi
done
echo "$content"
code="$(printf '%s' "$content" | sed -n 's/.*###DONE exit= *\(-\?[0-9]*\).*/\1/p' | tail -1)"
echo "REMOTE_EXIT=$code"

if [ -n "$FETCH" ]; then
  IFS=',' read -ra files <<< "$FETCH"
  mkdir -p evidence-out
  for f in "${files[@]}"; do
    f="$(printf '%s' "$f" | tr -d '\r')"
    if [[ "$f" == */ ]]; then
      f="${f%/}"
      base="$(basename "$f")"
      mkdir -p "evidence-out/$base"
      scp -q -r "${SSH[@]}" "$USER@$HOST:$f" "evidence-out/$base/" && echo "fetched=evidence-out/$base/" || echo "FETCH FAILED=$f"
    else
      base="$(basename "$f")"
      scp -q "${SSH[@]}" "$USER@$HOST:$f" "evidence-out/$base" && echo "fetched=evidence-out/$base" || echo "FETCH FAILED=$f"
    fi
  done
fi
exit "${code:-0}"
