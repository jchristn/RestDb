#!/usr/bin/env bash
# Builds and pushes the multi-architecture jchristn77/restdb image. Equivalent to build-server.bat.
set -euo pipefail

if [ "$#" -ne 1 ] || [ -z "$1" ]; then
  echo
  echo "Provide a tag argument."
  echo "Example: ./build-server.sh v2.0.0"
  exit 1
fi

cd "$(dirname "$0")"

echo
echo "Building for linux/amd64 and linux/arm64/v8..."
docker buildx build -f src/RestDb/Dockerfile --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/restdb:"$1" --tag jchristn77/restdb:latest --push src/RestDb

echo
echo "Done"
