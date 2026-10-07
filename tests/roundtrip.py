"""
Load a .json file and write it back out with MegaDetector's write_json(), for comparison with
"SpeciesNetPostprocessor.Cli --roundtrip".

Usage: roundtrip.py input.json output.json
"""

import json
import sys

from megadetector.utils.ct_utils import write_json

with open(sys.argv[1], 'r') as f:
    d = json.load(f)
write_json(sys.argv[2], d)
