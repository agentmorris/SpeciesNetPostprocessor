"""
Compare two .json files: first byte-for-byte, then (if they differ) semantically, reporting
the first few differences.

Usage: compare_json.py a.json b.json [max_differences]

Exits with 0 if the files are byte-for-byte identical, 1 otherwise.
"""

import json
import sys


def compare_json_files(fn_a, fn_b, max_differences=20):
    """
    Compares two .json files.

    Returns:
        tuple: (status, differences), where status is 'identical' (byte-for-byte identical),
        'semantically identical' (same content, different bytes), or 'different', and
        differences is a list of strings describing the first [max_differences] differences.
    """

    with open(fn_a, 'rb') as f:
        bytes_a = f.read()
    with open(fn_b, 'rb') as f:
        bytes_b = f.read()

    if bytes_a == bytes_b:
        return 'identical', []

    a = json.loads(bytes_a)
    b = json.loads(bytes_b)

    differences = []

    def compare(x, y, path):
        if len(differences) >= max_differences:
            return
        if type(x) is not type(y):
            differences.append('{}: type {} vs {} ({!r} vs {!r})'.format(
                path, type(x).__name__, type(y).__name__, x, y))
            return
        if isinstance(x, dict):
            if list(x.keys()) != list(y.keys()):
                differences.append('{}: keys {} vs {}'.format(
                    path, list(x.keys())[:20], list(y.keys())[:20]))
            for k in x:
                if k in y:
                    compare(x[k], y[k], path + '/' + str(k))
        elif isinstance(x, list):
            if len(x) != len(y):
                differences.append('{}: list length {} vs {}'.format(path, len(x), len(y)))
            for i, (xi, yi) in enumerate(zip(x, y)):
                p = path + '[{}]'.format(i)
                if isinstance(xi, dict) and 'file' in xi:
                    p = path + '[{}:{}]'.format(i, xi['file'])
                compare(xi, yi, p)
        elif x != y:
            differences.append('{}: {!r} vs {!r}'.format(path, x, y))

    compare(a, b, '')

    if len(differences) == 0:
        return 'semantically identical', []
    return 'different', differences


if __name__ == '__main__':

    max_differences = int(sys.argv[3]) if len(sys.argv) > 3 else 20
    status, differences = compare_json_files(sys.argv[1], sys.argv[2], max_differences)
    print(status.upper())
    for s in differences:
        print('  ' + s)
    sys.exit(0 if status == 'identical' else 1)
