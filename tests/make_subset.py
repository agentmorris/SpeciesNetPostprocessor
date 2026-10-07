"""
Create a subset of an MD-formatted results file containing all images from the first
[n_folders] folders (in order of first appearance), preserving all other fields.

Usage: make_subset.py input.json output.json n_folders
"""

import json
import os
import sys

input_fn, output_fn, n_folders = sys.argv[1], sys.argv[2], int(sys.argv[3])

with open(input_fn, 'r') as f:
    d = json.load(f)

folders = []
for im in d['images']:
    folder = os.path.dirname(im['file'].replace('\\', '/'))
    if folder not in folders:
        folders.append(folder)
    if len(folders) >= n_folders:
        break
folders = set(folders[:n_folders])

d['images'] = [im for im in d['images']
               if os.path.dirname(im['file'].replace('\\', '/')) in folders]

print('Kept {} images from {} folders'.format(len(d['images']), len(folders)))

with open(output_fn, 'w') as f:
    json.dump(d, f, indent=2)
