"""
Generate random MD-formatted results files, taxa lists, and options files that exercise the
postprocessing code paths (ties, related taxa, "other" categories, merged categories, non-animal
classifications, failed images, camera overflow folders, sequence boundaries, bad timestamps,
etc.), for comparing the C# port against the Python reference.

Generated inputs avoid things that make the MegaDetector Python code fail (e.g. unsorted or
empty classification lists); see make_edge_cases.py for those.

Usage: generate_fuzz_cases.py output_folder n_cases seed [--timestamp-mode exif|datetime|both|mixed]

Each case gets a folder containing input.json, options.json, and (sometimes) taxa.csv.
"""

import argparse
import csv
import json
import os
import random

from datetime import datetime, timedelta

parser = argparse.ArgumentParser()
parser.add_argument('output_folder')
parser.add_argument('n_cases', type=int)
parser.add_argument('seed', type=int)
parser.add_argument('--timestamp-mode', default=None, choices=['exif', 'datetime', 'both', 'mixed'],
                    help='force all cases to use this timestamp mode (default: random)')
args = parser.parse_args()

output_folder = args.output_folder
n_cases = args.n_cases
seed = args.seed

repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
taxonomy_file = os.path.join(repo_root, 'src', 'SpeciesNetPostprocessor.Core', 'Resources',
                             'speciesnet_taxonomy_release_20260609.txt')
with open(taxonomy_file, 'r', encoding='utf-8') as f:
    taxonomy_lines = [s.strip() for s in f if len(s.strip()) > 0]

no_cv_result = 'f2efdae9-efb8-48fb-8a91-eccf79ab4ffb;no cv result;no cv result;no cv result;' \
               'no cv result;no cv result;no cv result'
human = '990ae9dd-7a59-4344-afcb-1b7b21368000;mammalia;primates;hominidae;homo;sapiens;human'


def latin_name(s):
    tokens = s.split(';')
    cls, order, family, genus, species = tokens[1:6]
    if len(cls) == 0:
        return None
    if len(species) > 0:
        return genus + ' ' + species
    for t in (genus, family, order, cls):
        if len(t) > 0:
            return t
    return None


taxa = [s for s in taxonomy_lines if latin_name(s) is not None and s != human]
by_family = {}
for s in taxa:
    family = s.split(';')[3]
    if len(family) > 0:
        by_family.setdefault(family, []).append(s)
higher_level = [s for s in taxa if len(s.split(';')[3]) == 0]
special = {
    'blank': [s for s in taxonomy_lines if s.endswith(';;;;;;blank')][0],
    'animal': [s for s in taxonomy_lines if s.endswith(';;;;;;animal')][0],
    'vehicle': [s for s in taxonomy_lines if s.endswith(';;;;;;vehicle')][0],
}
favorite_families = ['bovidae', 'equidae', 'felidae', 'canidae', 'hyaenidae', 'suidae',
                     'cercopithecidae', 'cervidae', 'numididae', 'herpestidae']

made_up_names = ['antelope', 'big cat', 'small cat', 'dog thing', 'ungulate', 'bird', 'critter',
                 'striped horse', 'pig', 'monkey', 'zebraplains', 'wildebeestblue', 'other']


def make_case(i_case, rng):

    case_folder = os.path.join(output_folder, 'case_{:03d}'.format(i_case))
    os.makedirs(case_folder, exist_ok=True)

    # Choose categories

    families = rng.sample(favorite_families, rng.randint(2, 4)) + \
        rng.sample(sorted(by_family.keys()), rng.randint(1, 3))
    descriptions = []
    for family in families:
        members = by_family.get(family, [])
        if len(members) == 0:
            continue
        descriptions.extend(rng.sample(members, min(len(members), rng.randint(2, 7))))
    descriptions.extend(rng.sample(higher_level, rng.randint(2, 6)))
    for name in ('blank', 'animal', 'vehicle'):
        if rng.random() < 0.8:
            descriptions.append(special[name])
    if rng.random() < 0.7:
        descriptions.append(human)
    if rng.random() < 0.3:
        descriptions.append(no_cv_result)
    descriptions = list(dict.fromkeys(descriptions))
    rng.shuffle(descriptions)

    category_ids = [str(i + 1) for i in range(len(descriptions))]
    if rng.random() < 0.3:
        rng.shuffle(category_ids)
    classification_categories = {}
    classification_category_descriptions = {}
    for cid, desc in zip(category_ids, descriptions):
        classification_categories[cid] = desc.split(';')[-1]
        classification_category_descriptions[cid] = desc
    desc_to_id = {desc: cid for cid, desc in zip(category_ids, descriptions)}

    taxonomic_ids = [desc_to_id[s] for s in descriptions if latin_name(s) is not None and s != no_cv_result]
    other_ids = [desc_to_id[s] for s in descriptions if s not in taxonomic_ids and
                 desc_to_id[s] not in taxonomic_ids]

    def related_ids(cid):
        # Categories that share a family (or order) with [cid]
        tokens = classification_category_descriptions[cid].split(';')
        result = []
        for other_cid in taxonomic_ids:
            other_tokens = classification_category_descriptions[other_cid].split(';')
            if (len(tokens[3]) > 0 and tokens[3] == other_tokens[3]) or \
                    (len(tokens[2]) > 0 and tokens[2] == other_tokens[2] and len(other_tokens[3]) == 0) or \
                    (tokens[1] == other_tokens[1] and len(other_tokens[2]) == 0):
                result.append(other_cid)
        return result

    # Generate images

    images = []
    n_cameras = rng.randint(2, 7)
    timestamp_mode = rng.choice(['exif', 'exif', 'datetime', 'both', 'mixed'])
    if args.timestamp_mode is not None:
        timestamp_mode = args.timestamp_mode

    for i_camera in range(n_cameras):

        r = rng.random()
        if r < 0.1 and i_camera == 0:
            camera_folders = ['']
        elif r < 0.3:
            camera_folders = ['site{}/cam{}/{}RECNX'.format(i_camera, i_camera, 100 + k) for k in range(2)]
        elif r < 0.4:
            camera_folders = ['site{}/c\u00e1mara {}'.format(i_camera, i_camera)]
        else:
            camera_folders = ['site{}/cam{}'.format(i_camera // 2, i_camera)]

        t = datetime(2022, 1, 1) + timedelta(seconds=rng.randint(0, 300 * 24 * 3600))
        i_image = 0

        for i_event in range(rng.randint(2, 12)):

            true_cid = rng.choice(taxonomic_ids)
            related = related_ids(true_cid)
            n_images_this_event = rng.randint(1, 8)

            for i_frame in range(n_images_this_event):

                if i_frame > 0:
                    t = t + timedelta(seconds=rng.choice([0, 1, 1, 2, 5, 10, 30, 59, 60, 60, 61, 90]))
                folder = rng.choice(camera_folders)
                fn = 'IMG_{:04d}.JPG'.format(i_image)
                i_image += 1
                file = fn if folder == '' else folder + '/' + fn
                if rng.random() < 0.1:
                    file = file.replace('/', '\\')

                im = {'file': file}

                # Timestamps
                ts = t.strftime('%Y:%m:%d %H:%M:%S')
                r = rng.random()
                if r < 0.03:
                    ts = '0000:00:00 00:00:00'
                elif r < 0.05:
                    ts = '1999:12:31 23:59:59'
                elif r < 0.06:
                    ts = '2099:01:01 00:00:00'
                elif r < 0.07:
                    ts = '2022:2:3 4:05:06'
                mode = timestamp_mode if timestamp_mode != 'mixed' else rng.choice(['exif', 'datetime', 'none'])
                if mode in ('exif', 'both'):
                    im['exif_metadata'] = {'Make': 'TestCam', 'DateTimeOriginal': ts}
                if mode in ('datetime', 'both'):
                    im['datetime'] = ts

                # Failures
                if rng.random() < 0.03:
                    im['failure'] = 'image access failure'
                    if rng.random() < 0.5:
                        im['detections'] = None
                    images.append(im)
                    continue

                detections = []
                for i_det in range(rng.choice([0, 1, 1, 2, 3, 4, 6, 9])):
                    det = {}
                    det['category'] = rng.choice(['1'] * 8 + ['2', '3'])
                    det['conf'] = rng.choice([round(rng.random(), 3), 0.15, 0.5, 0.149, 0.9])
                    det['bbox'] = [round(rng.random(), 4) for _ in range(4)]
                    det['verified'] = False
                    r = rng.random()
                    if r < 0.85:
                        top = None
                        r2 = rng.random()
                        if r2 < 0.55:
                            top = true_cid
                        elif r2 < 0.75 and len(related) > 0:
                            top = rng.choice(related)
                        elif r2 < 0.9 and len(other_ids) > 0:
                            top = rng.choice(other_ids)
                        else:
                            top = rng.choice(category_ids)
                        n_classifications = rng.choice([1, 1, 1, 2, 3, 5])
                        confs = sorted([rng.choice([round(rng.random(), 4), 0.5, 0.4999])
                                        for _ in range(n_classifications)], reverse=True)
                        cids = [top] + rng.sample(category_ids, n_classifications - 1)
                        det['classifications'] = [[c, conf] for c, conf in zip(cids, confs)]
                    detections.append(det)

                im['detections'] = detections
                im['width'] = 2048
                images.append(im)

        # ...for each event

    # ...for each camera

    rng.shuffle(images)

    d = {
        'info': {'format_version': '1.5', 'detector': 'md_v5a.0.0.pt', 'classifier': 'speciesnet'},
        'detection_categories': {'1': 'animal', '2': 'person', '3': 'vehicle'},
        'classification_categories': classification_categories,
        'classification_category_descriptions': classification_category_descriptions,
        'images': images
    }
    if rng.random() < 0.3:
        d['extra_field'] = {'something': [1, 2.5, 'three']}

    with open(os.path.join(case_folder, 'input.json'), 'w') as f:
        json.dump(d, f, indent=1)

    # Options

    advanced = {
        'allow_walk_down': rng.random() < 0.4,
        'use_original_common_names_if_available': rng.random() < 0.8,
        'classification_confidence_threshold': rng.choice([0.5, 0.5, 0.3, 0.7]),
        'detection_confidence_threshold': rng.choice([0.15, 0.15, 0.1, 0.3]),
        'min_detections_to_overwrite_secondary': rng.choice([4, 4, 2, 1]),
        'max_detections_nondominant_class': rng.choice([1, 1, 2, 3]),
        'min_detections_to_overwrite_other': rng.choice([2, 2, 1, 3]),
        'propagate_classifications_through_taxonomy': rng.random() < 0.8,
        'allow_same_family_smoothing': rng.random() < 0.4,
        'min_detections_to_overwrite_secondary_same_family': rng.choice([2, 2, 1, 3]),
        'episode_interval_seconds': rng.choice([60.0, 60.0, 30.0, 120.0]),
        'include_description_fields': rng.random() < 0.8,
        'remove_unused_categories': rng.random() < 0.8,
    }
    if rng.random() < 0.2:
        advanced['other_category_names'] = ['blank', 'animal']

    options = {
        'remove_classifications_from_non_animals': rng.random() < 0.7,
        'apply_taxa_list': rng.random() < 0.8,
        'taxa_list_source': 'built_in',
        'built_in_taxa_list': 'Snapshot Serengeti',
        'apply_within_image_smoothing': rng.random() < 0.8,
        'apply_sequence_level_smoothing': rng.random() < 0.8,
        'write_log_file': True,
        'advanced': advanced
    }

    # Possibly a random taxa list

    if options['apply_taxa_list'] and rng.random() < 0.6:
        candidates = sorted(set(latin_name(s) for s in descriptions
                                if latin_name(s) is not None and s != no_cv_result))
        # Also add parents of the taxa in this file, and some random taxa
        for s in descriptions:
            tokens = s.split(';')
            for t in tokens[1:5]:
                if len(t) > 0 and t != 'no cv result' and any(latin_name(x) == t for x in taxa):
                    candidates.append(t)
        candidates.extend(latin_name(s) for s in rng.sample(taxa, 5))
        candidates = sorted(set(candidates))
        chosen = rng.sample(candidates, min(len(candidates), rng.randint(3, 12)))
        rows = []
        for latin in chosen:
            row = {'latin': latin, 'common': rng.choice(made_up_names)}
            if rng.random() < 0.5:
                row['original_common'] = rng.choice(made_up_names + [''])
            rows.append(row)
        if rng.random() < 0.3 and len(rows) > 0:
            rows.append({'latin': rows[0]['latin'], 'common': 'duplicate entry', 'original_common': 'dupe'})
        if rng.random() < 0.3:
            rows.append({'latin': '', 'common': '', 'original_common': 'fire'})
        csv_fn = os.path.join(case_folder, 'taxa.csv')
        with open(csv_fn, 'w', newline='') as f:
            writer = csv.DictWriter(f, fieldnames=['latin', 'common', 'original_common'])
            writer.writeheader()
            for row in rows:
                writer.writerow(row)
        options['taxa_list_source'] = 'csv_file'
        options['taxa_list_file'] = csv_fn

    with open(os.path.join(case_folder, 'options.json'), 'w') as f:
        json.dump(options, f, indent=1)


rng = random.Random(seed)
for i_case in range(n_cases):
    make_case(i_case, rng)
print('Generated {} cases in {}'.format(n_cases, output_folder))
