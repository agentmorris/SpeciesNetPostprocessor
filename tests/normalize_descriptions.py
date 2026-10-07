"""
Pre-normalize an MD-formatted results file so the MegaDetector Python code can process it
the same way the C# port does: fill in missing classification category descriptions (with
standard SpeciesNet strings for well-known categories), and replace the first token of
human / no-cv-result descriptions with the standard SpeciesNet GUIDs (the Python code
identifies those categories by exact string match).

Usage: normalize_descriptions.py input.json output.json
"""

import json
import sys

from megadetector.utils.ct_utils import write_json

standard = {
    'blank': 'f1856211-cfb7-4a5b-9158-c0f72fd09ee6;;;;;;blank',
    'animal': '1f689929-883d-4dae-958c-3d57ab5b6c16;;;;;;animal',
    'vehicle': 'e2895ed5-780b-48f6-8a11-9e27cb594511;;;;;;vehicle',
    'no cv result': 'f2efdae9-efb8-48fb-8a91-eccf79ab4ffb;no cv result;no cv result;no cv result;'
                    'no cv result;no cv result;no cv result',
    'human': '990ae9dd-7a59-4344-afcb-1b7b21368000;mammalia;primates;hominidae;homo;sapiens;human',
}
short_to_standard = {';'.join(v.split(';')[1:-1]): v for k, v in standard.items()
                     if k in ('human', 'no cv result')}

with open(sys.argv[1], 'r') as f:
    d = json.load(f)

cats = d['classification_categories']
descs = d.get('classification_category_descriptions', {})

new_descs = {}
for k, name in cats.items():
    if k in descs:
        desc = descs[k]
        short = ';'.join(desc.split(';')[1:-1])
        if short in short_to_standard:
            desc = short_to_standard[short]
    else:
        n = name.strip().lower()
        desc = standard.get(n, '{};;;;;;{}'.format(n, n))
    new_descs[k] = desc

d['classification_category_descriptions'] = new_descs
write_json(sys.argv[2], d)
print('Wrote {}'.format(sys.argv[2]))
