#!/usr/bin/env bash
# Regenerate the helper library used by the real-machine checks straight out of the CI visual
# evidence harness, so the two never drift apart: everything the checks share (WinEnum P/Invoke,
# screenshot/UIA/tray/mock/config helpers) already exists in visual-evidence.ps1; the checks only
# need the preamble + helper functions, i.e. lines 1..1401 plus the UIA dump helpers 4723..4795.
#
# usage: tools/realmachine/extract-lib.sh > tools/realmachine/lib.ps1   (or pipe to a file)
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
src="$root/.github/workflows/scripts/visual-evidence.ps1"
{ sed -n '1,1401p' "$src"; sed -n '4723,4795p' "$src"; } > /tmp/beacon-lib.ps1
# keep the UTF-8 BOM PowerShell 5.1 needs for the Chinese comments/strings
if ! head -c 3 /tmp/beacon-lib.ps1 | grep -q $'\xef\xbb\xbf'; then
  printf '\xef\xbb\xbf' > /tmp/beacon-lib.bom
  cat /tmp/beacon-lib.ps1 >> /tmp/beacon-lib.bom
  mv /tmp/beacon-lib.bom /tmp/beacon-lib.ps1
fi
cat /tmp/beacon-lib.ps1
