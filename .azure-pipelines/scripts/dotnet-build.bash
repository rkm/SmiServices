#!/usr/bin/env bash

set -euo pipefail

dotnet restore --locked-mode

dotnet build \
    -p:Platform=x64 \
    --configuration Release \
    --verbosity quiet
