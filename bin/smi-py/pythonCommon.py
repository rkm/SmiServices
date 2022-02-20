"""Common python functions and variables"""

import os
import sys

# TODO(rkm 2022-02-25) This sucks
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))
import common as C

PY_DIR = C.PROJ_ROOT / "src/common/Smi_Common_Python"
