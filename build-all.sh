#!/usr/bin/env bash
# Builds and pushes the server, dashboard, and MCP images. Equivalent to build-all.bat.
set -euo pipefail

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo
  echo "Provide exactly one image tag argument."
  echo "Example: ./build-all.sh v2.0.0"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

"$SCRIPT_DIR/build-server.sh" "$1"
"$SCRIPT_DIR/build-dashboard.sh" "$1"
"$SCRIPT_DIR/build-mcp.sh" "$1"

echo
echo "Done"
