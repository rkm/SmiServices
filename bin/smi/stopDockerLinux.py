#!/usr/bin/env python3

import argparse
import os
import shlex
import sys

# TODO(rkm 2022-02-25) This sucks
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))
import common as C

_COMPOSE_FILE_NAME = "linux-dotnet.yml"
_COMPOSE_FILE_PATH = (C.PROJ_ROOT / "utils/docker-compose" / _COMPOSE_FILE_NAME).resolve()
assert _COMPOSE_FILE_PATH.is_file()

def main() -> int:
    
    parser = C.get_docker_parser()
    parser.add_argument(
        "db_password",
    )
    args = parser.parse_args()

    docker = "docker" if not args.podman else "podman"

    cmd = (
        f"{docker}-compose", 
        "-f", _COMPOSE_FILE_PATH,
        "down",
        "--timeout", 0,
    )
    C.run(cmd)

    return 0

if __name__ == "__main__":
    raise SystemExit(main())