"""
Run the postprocessing steps from MegaDetector's manage_local_batch.py notebook (custom taxa
list, remove non-animal classifications, within-image smoothing, sequence-level smoothing,
remove unused categories) on an MD-formatted results file, using the MegaDetector Python
package, to produce reference output for the C# port.

Options are read from the same snake_case .json options file the C# command-line tool uses
(see "SpeciesNetPostprocessor.Cli --write-default-options").

Sequence information is built from exif_metadata/DateTimeOriginal (or "datetime") in the
results file, rather than by reading EXIF data from images, otherwise mirroring the notebook.

Run with "python -X utf8", so the SpeciesNet taxonomy file is read as UTF-8 on Windows.

Usage: python_reference.py input.json output.json [options.json|-] [intermediate_folder]
"""

import json
import os
import sys
import tempfile

from datetime import datetime

from megadetector.postprocessing.classification_postprocessing import \
    restrict_to_taxa_list, remove_classifications_from_non_animal_detections, \
    smooth_classification_results_image_level, smooth_classification_results_sequence_level, \
    ClassificationSmoothingOptions
from megadetector.data_management.read_exif import exif_results_to_cct, ExifResultsToCCTOptions
from megadetector.data_management import cct_json_utils
from megadetector.data_management.cct_json_utils import SequenceOptions
from megadetector.postprocessing.subset_json_detector_output import \
    SubsetJsonDetectorOutputOptions, subset_json_detector_output
from megadetector.utils.ct_utils import write_json

repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
resources_folder = os.path.join(repo_root, 'src', 'SpeciesNetPostprocessor.Core', 'Resources')
builtin_taxa_lists = {
    'Snapshot Serengeti': os.path.join(resources_folder, 'snapshot_serengeti_speciesnet_20251031.csv')
}
builtin_taxonomy_file = os.path.join(resources_folder, 'speciesnet_taxonomy_release_20260609.txt')

input_file = sys.argv[1]
output_file = sys.argv[2]
options = {}
if len(sys.argv) > 3 and sys.argv[3] != '-':
    with open(sys.argv[3], 'r') as f:
        options = json.load(f)
intermediate_folder = sys.argv[4] if len(sys.argv) > 4 else tempfile.mkdtemp(prefix='python-reference-')
os.makedirs(intermediate_folder, exist_ok=True)

advanced = options.get('advanced', {})


def opt(name, default):
    return options.get(name, default)


def adv(name, default):
    return advanced.get(name, default)


current_file = input_file
step_index = 0


def next_file(name):
    global step_index
    step_index += 1
    return os.path.join(intermediate_folder, 'step{}_{}.json'.format(step_index, name))


def smoothing_options():
    o = ClassificationSmoothingOptions()
    o.classification_confidence_threshold = adv('classification_confidence_threshold', 0.5)
    o.detection_confidence_threshold = adv('detection_confidence_threshold', 0.15)
    o.min_detections_to_overwrite_secondary = adv('min_detections_to_overwrite_secondary', 4)
    o.max_detections_nondominant_class = adv('max_detections_nondominant_class', 1)
    o.min_detections_to_overwrite_other = adv('min_detections_to_overwrite_other', 2)
    o.other_category_names = adv('other_category_names', o.other_category_names)
    o.propagate_classifications_through_taxonomy = adv('propagate_classifications_through_taxonomy', True)
    # This is how the notebook implements allow_same_family_smoothing
    if adv('allow_same_family_smoothing', False):
        o.max_detections_nondominant_class_same_family = 10000
    o.min_detections_to_overwrite_secondary_same_family = \
        adv('min_detections_to_overwrite_secondary_same_family', 2)
    o.taxonomy_propagation_level_weight = adv('taxonomy_propagation_level_weight', 1.0)
    o.taxonomy_propagation_count_weight = adv('taxonomy_propagation_count_weight', 0.01)
    o.add_pre_smoothing_description = adv('include_description_fields', True)
    return o


#%% Custom taxa list

if opt('apply_taxa_list', True):

    if opt('taxa_list_source', 'built_in') == 'built_in':
        taxa_list = builtin_taxa_lists[opt('built_in_taxa_list', 'Snapshot Serengeti')]
    else:
        taxa_list = opt('taxa_list_file', None)
    taxonomy_file = adv('taxonomy_file', None) or builtin_taxonomy_file
    include_descriptions = adv('include_description_fields', True)

    fn = next_file('custom_taxa')
    restrict_to_taxa_list(taxa_list=taxa_list,
                          speciesnet_taxonomy_file=taxonomy_file,
                          input_file=current_file,
                          output_file=fn,
                          allow_walk_down=adv('allow_walk_down', False),
                          add_pre_filtering_description=include_descriptions,
                          add_post_filtering_description=include_descriptions,
                          use_original_common_names_if_available=adv('use_original_common_names_if_available', True))
    current_file = fn


#%% Remove classifications from non-animal detections

if opt('remove_classifications_from_non_animals', True):

    fn = next_file('animal_classifications_only')
    remove_classifications_from_non_animal_detections(current_file, fn)
    current_file = fn


#%% Within-image smoothing

if opt('apply_within_image_smoothing', True):

    fn = next_file('within_image_smoothing')
    _ = smooth_classification_results_image_level(input_file=current_file,
                                                  output_file=fn,
                                                  options=smoothing_options())
    current_file = fn


#%% Sequence-level smoothing

def parse_exif_datetime(s):
    try:
        return datetime.strptime(s, '%Y:%m:%d %H:%M:%S')
    except Exception:
        return None


if opt('apply_sequence_level_smoothing', True):

    # Build "EXIF results" from the results file itself, sorted by filename like find_images()
    with open(current_file, 'r') as f:
        d = json.load(f)
    exif_results = []
    for im in d['images']:
        r = {'file_name': im['file']}
        candidates = []
        if 'exif_metadata' in im and isinstance(im['exif_metadata'], dict) and \
                isinstance(im['exif_metadata'].get('DateTimeOriginal'), str):
            candidates.append(im['exif_metadata']['DateTimeOriginal'])
        if isinstance(im.get('datetime'), str):
            candidates.append(im['datetime'])
        # Mirror the C# behavior of falling back to "datetime" if DateTimeOriginal can't be parsed
        chosen = None
        for s in candidates:
            if parse_exif_datetime(s) is not None:
                chosen = s
                break
        if chosen is None and len(candidates) > 0:
            chosen = candidates[0]
        if chosen is not None:
            r['exif_tags'] = {'DateTimeOriginal': chosen}
        exif_results.append(r)
    exif_results = sorted(exif_results, key=lambda r: r['file_name'].replace('\\', '/'))

    cct_dict = exif_results_to_cct(exif_results=exif_results,
                                   cct_output_file=None,
                                   options=ExifResultsToCCTOptions())
    sequence_options = SequenceOptions()
    sequence_options.episode_interval_seconds = adv('episode_interval_seconds', 60.0)
    _ = cct_json_utils.create_sequences(cct_dict, options=sequence_options)

    fn = next_file('seqsmoothing')
    _ = smooth_classification_results_sequence_level(input_file=current_file,
                                                     cct_sequence_information=cct_dict,
                                                     output_file=fn,
                                                     options=smoothing_options())
    current_file = fn


#%% Remove unused categories

if adv('remove_unused_categories', True):

    fn = next_file('trimmed')
    subset_options = SubsetJsonDetectorOutputOptions()
    subset_options.remove_classification_categories_below_count = 1
    subset_options.overwrite_json_files = True
    _ = subset_json_detector_output(current_file, fn, subset_options)
    current_file = fn


#%% Write output

# Re-written with write_json(), so formatting is consistent even if no steps ran
with open(current_file, 'r') as f:
    d = json.load(f)
write_json(output_file, d)
print('Wrote reference output to {}'.format(output_file))
