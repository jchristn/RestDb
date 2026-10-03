#!/usr/bin/env bash
# Builds and pushes the multi-architecture jchristn77/restdb-dashboard image. Equivalent to build-dashboard.bat.
set -euo pipefail

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo
  echo "Provide a tag argument."
  echo "Example: ./build-dashboard.sh v2.0.0"
  exit 1
fi

cd "$(dirname "$0")"

echo
echo "Building for linux/amd64 and linux/arm64/v8..."
docker buildx build -f src/RestDb.Dashboard/Dockerfile --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/restdb-dashboard:"$1" --tag jchristn77/restdb-dashboard:latest --push src/RestDb.Dashboard

echo
echo "Done"
