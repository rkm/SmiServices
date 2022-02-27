#!/usr/bin/env python3

"""\
    f="tests/common/Smi.Common.Tests/RelationalDatabases.yaml"
        cat > "f" << EOF
        MySql: 'server=127.0.0.1;Uid=root;Pwd=YourStrongPassw0rd;sslmode=None'
        SqlServer: 'Server=localhost;User Id=sa;Password=${{ env.db-password }};TrustServerCertificate=true;'
        EOF
        cat "$f"

        f="./tests/common/Smi.Common.Tests/TestDatabases.txt"
        cat > "f" << EOF
        ServerName: localhost
        Prefix: TEST_
        Username: sa
        Password: ${{ env.db-password }}
        MySql: server=127.0.0.1;Uid=root;Pwd=${{ env.db-password }};sslmode=None
        EOF
        cat "$f"
"""

import argparse
import os
import sys

# TODO(rkm 2022-02-25) This sucks
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))
import common as C

_RELATIONAL_YAML = (C.PROJ_ROOT / "tests/common/Smi.Common.Tests/RelationalDatabases.yaml").resolve()
assert _RELATIONAL_YAML.is_file()

_TEST_DBS_TXT = (C.PROJ_ROOT / "tests/common/Smi.Common.Tests/TestDatabases.txt").resolve()
assert _TEST_DBS_TXT.is_file()


def main() -> int:

    parser = argparse.ArgumentParser()
    parser.add_argument(
        "mssql_server",
    )
    parser.add_argument(
        "db_password",
    )
    args = parser.parse_args()

    with open(_RELATIONAL_YAML, "w") as f:
        if "localdb" in args.mssql_server:
            f.write(f"SqlServer: 'Server={args.mssql_server};'\n")
        else:
            f.write(f"SqlServer: 'Server={args.mssql_server};User Id=sa;Password={args.db_password};TrustServerCertificate=true;'\n")
        f.write(f"MySql: 'server=127.0.0.1;Uid=root;Pwd={args.db_password};sslmode=None'\n")

    with open(_RELATIONAL_YAML) as f:
        print(f"{_RELATIONAL_YAML}:")
        print(f.read())

    with open(_TEST_DBS_TXT, "w") as f:
        f.write(f"ServerName: {args.mssql_server}\n")
        f.write("Prefix: TEST_\n")
        f.write("Username: sa\n")
        f.write(f"Password: {args.db_password}\n")
        f.write(f"MySql: server=127.0.0.1;Uid=root;Pwd={args.db_password};sslmode=None\n")

    with open(_TEST_DBS_TXT) as f:
        print(f"{_TEST_DBS_TXT}:")
        print(f.read())

    return 0

if __name__ == "__main__":
    raise SystemExit(main())