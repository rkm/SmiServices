#!/usr/bin/env bash

set -euxo pipefail

base=$(pwd)/.azure-pipelines/docker-compose/
dockerfiles=$(find "$base" -type f | grep -v lock)

for f in $dockerfiles
do
    f=$(basename "$f")
    docker run \
        --rm \
        -v"$base":/run \
        --user "$(id -u)":"$(id -g)" \
        safewaters/docker-lock \
            lock generate \
            --composefiles "$f" \
            --lockfile-name "$f".lock
done
