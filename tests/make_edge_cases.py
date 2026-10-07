"""
Create edge-case inputs for the C# port, mostly cases where the MegaDetector Python code would
fail, along with the expected outcome for each (see EXPECTATIONS).  These can't be compared
to the Python code, so run_comparisons.py checks the exit code and console output of the C#
command-line tool instead.

Usage: make_edge_cases.py base_input.json output_folder

[base_input.json] should be a SpeciesNet results file with timestamps and classifications.
"""

import copy
import json
import os
import sys

# Maps each edge case to (expected exit code, text that should appear in the console output)
EXPECTATIONS = {
    'unsorted_and_empty_classifications': (0, 'were not sorted by confidence'),
    'uppercase_descriptions': (0, 'converted to lowercase'),
    'addaxai_style_descriptions': (0, 'using the standard SpeciesNet description'),
    'unknown_category_without_description': (0, 'treating it as a non-taxonomic category'),
    'taxon_not_in_taxonomy': (0, 'not in the SpeciesNet taxonomy'),
    'with_bom': (0, 'Finished processing'),
    'duplicate_filenames': (1, 'appear more than once'),
    'no_timestamps': (1, 'requires image timestamps'),
    'no_classifications': (1, "doesn't contain any species classification results"),
    'empty_images': (1, "doesn't contain any images"),
    'undefined_classification_category': (1, 'which is not in the classification category list'),
    'not_a_results_file': (1, "doesn't look like a MegaDetector/SpeciesNet results file"),
    'truncated': (1, 'does not appear to be a valid .json file'),
}


def _classified_detections(d):
    for im in d['images']:
        for det in (im.get('detections') or []):
            if det.get('classifications'):
                yield det


def make_edge_cases(base_fn, output_folder):
    """
    Writes one .json file per key in EXPECTATIONS to [output_folder].
    """

    os.makedirs(output_folder, exist_ok=True)

    with open(base_fn, 'r') as f:
        base = json.load(f)

    def write(name, d, bom=False):
        assert name in EXPECTATIONS
        fn = os.path.join(output_folder, name + '.json')
        with open(fn, 'w', encoding='utf-8-sig' if bom else 'utf-8') as f:
            json.dump(d, f, indent=1)

    # Unsorted classification lists, and empty classification lists
    d = copy.deepcopy(base)
    cids = list(d['classification_categories'].keys())
    for i, det in enumerate(_classified_detections(d)):
        if i % 7 == 0:
            det['classifications'] = [[cids[0], 0.01], det['classifications'][0], [cids[1], 0.005]]
        elif i % 11 == 0:
            det['classifications'] = []
    write('unsorted_and_empty_classifications', d)

    # Uppercase descriptions
    d = copy.deepcopy(base)
    for k in list(d['classification_category_descriptions'].keys())[:5]:
        d['classification_category_descriptions'][k] = d['classification_category_descriptions'][k].upper()
    write('uppercase_descriptions', d)

    # AddaxAI-style descriptions: common names in place of GUIDs, and no description for "blank"
    d = copy.deepcopy(base)
    descriptions = d['classification_category_descriptions']
    for k in descriptions:
        tokens = descriptions[k].split(';')
        tokens[0] = tokens[-1]
        descriptions[k] = ';'.join(tokens)
    blank_ids = [k for k, v in d['classification_categories'].items() if v == 'blank']
    if len(blank_ids) == 0:
        blank_ids = ['997']
        d['classification_categories']['997'] = 'blank'
        for i, det in enumerate(_classified_detections(d)):
            if i % 9 == 0:
                det['classifications'][0][0] = '997'
    descriptions.pop(blank_ids[0], None)
    write('addaxai_style_descriptions', d)

    # An unknown category with no description
    d = copy.deepcopy(base)
    d['classification_categories']['999'] = 'Mystery Thing'
    for i, det in enumerate(_classified_detections(d)):
        if i % 13 == 0:
            det['classifications'][0][0] = '999'
    write('unknown_category_without_description', d)

    # A taxon that isn't in the SpeciesNet taxonomy
    d = copy.deepcopy(base)
    d['classification_categories']['998'] = 'made-up beast'
    d['classification_category_descriptions']['998'] = \
        'aaaaaaaa-0000-0000-0000-000000000000;mammalia;fakeorder;fakefamily;fakegenus;fakespecies;made-up beast'
    for i, det in enumerate(_classified_detections(d)):
        if i % 17 == 0:
            det['classifications'][0][0] = '998'
    write('taxon_not_in_taxonomy', d)

    # A UTF-8 byte order mark
    write('with_bom', copy.deepcopy(base), bom=True)

    # Duplicate filenames
    d = copy.deepcopy(base)
    d['images'].append(copy.deepcopy(d['images'][0]))
    write('duplicate_filenames', d)

    # No timestamps
    d = copy.deepcopy(base)
    for im in d['images']:
        im.pop('exif_metadata', None)
        im.pop('datetime', None)
    write('no_timestamps', d)

    # MD-only file (no classifications)
    d = copy.deepcopy(base)
    d.pop('classification_categories', None)
    d.pop('classification_category_descriptions', None)
    for im in d['images']:
        for det in (im.get('detections') or []):
            det.pop('classifications', None)
    write('no_classifications', d)

    # Empty images list
    d = copy.deepcopy(base)
    d['images'] = []
    write('empty_images', d)

    # A detection referencing a classification category that doesn't exist
    d = copy.deepcopy(base)
    next(_classified_detections(d))['classifications'][0][0] = '12345'
    write('undefined_classification_category', d)

    # Not a results file
    with open(os.path.join(output_folder, 'not_a_results_file.json'), 'w') as f:
        json.dump([1, 2, 3], f)

    # A truncated file
    with open(os.path.join(output_folder, 'truncated.json'), 'w') as f:
        f.write(json.dumps(base, indent=1)[:5000])


if __name__ == '__main__':
    make_edge_cases(sys.argv[1], sys.argv[2])
