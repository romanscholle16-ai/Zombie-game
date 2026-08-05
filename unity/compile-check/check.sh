#!/usr/bin/env bash
# Type-checks every Unity gameplay script without opening the editor.
#
#   unity/compile-check/check.sh
#
# Needs a C# compiler: mcs (mono-mcs) or csc. UnityStubs.cs stands in for the
# slice of the UnityEngine API the game uses; it lives outside Assets/ so Unity
# never sees it.
set -euo pipefail
cd "$(dirname "$0")/../.."

CSC="${CSC:-mcs}"
command -v "$CSC" >/dev/null || { echo "No C# compiler found (set CSC=... or install mono-mcs)"; exit 1; }

"$CSC" -target:library -out:/tmp/rotgrid-check.dll \
  -nowarn:0169,0414,0649,0219,0168 \
  $(find unity/Rotgrid/Assets/Scripts -name '*.cs') \
  unity/compile-check/UnityStubs.cs

echo "OK — all Unity scripts type-check."
