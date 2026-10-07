"""
Compare the SpeciesNet Postprocessor (via its command-line tool) to the MegaDetector Python
code it was ported from, and check its handling of edge cases.

* For each real results file supplied with --input, and each option set in OPTION_SETS:
  run python_reference.py and the C# command-line tool, and check that the outputs are
  byte-for-byte identical.  Also checks that a JSON round-trip (read and re-write) matches
  MegaDetector's write_json().  Inputs are first passed through normalize_descriptions.py, so
  files the Python code can't process (e.g. from AddaxAI) can still be compared.

* Generates --n-fuzz-cases random results files (generate_fuzz_cases.py), each with random
  options, and compares outputs the same way.

* Generates edge cases (make_edge_cases.py) and checks the C# tool's exit code and output.

All output goes to --working-folder.  Requires the megadetector Python package in the
Python environment used to run this script (or the one specified with --python).

Usage:

  python run_comparisons.py --working-folder <dir> [--input results.json ...]
      [--cli SpeciesNetPostprocessor.Cli.exe] [--n-fuzz-cases 50] [--seed 0] [--skip-edge-cases]

Exits with 0 if everything passed.
"""

import argparse
import json
import os
import shutil
import subprocess
import sys
import time

# Don't leave __pycache__ folders in the repo when importing the modules below
sys.dont_write_bytecode = True

from compare_json import compare_json_files  # noqa: E402
from make_edge_cases import EXPECTATIONS, make_edge_cases  # noqa: E402

tests_folder = os.path.dirname(os.path.abspath(__file__))
repo_root = os.path.dirname(tests_folder)
default_cli = os.path.join(repo_root, 'src', 'SpeciesNetPostprocessor.Cli', 'bin', 'Release', 'net10.0',
                           'SpeciesNetPostprocessor.Cli.exe')

# Option sets used for each --input file; keys are the C# options file format (snake_case
# versions of PostprocessingOptions), and anything not specified uses the default
OPTION_SETS = {
    'default': {},
    'walkdown_samefamily': {
        'advanced': {'allow_walk_down': True, 'allow_same_family_smoothing': True}
    },
    'no_taxa': {
        'apply_taxa_list': False
    },
    'thresholds': {
        'remove_classifications_from_non_animals': False,
        'advanced': {
            'classification_confidence_threshold': 0.3,
            'detection_confidence_threshold': 0.1,
            'max_detections_nondominant_class': 3,
            'episode_interval_seconds': 120.0,
            'include_description_fields': False
        }
    }
}


class Runner:

    def __init__(self, args):
        self.python = args.python
        self.cli = args.cli
        self.failures = []
        self.n_passed = 0

    def run_python(self, script, script_args, log_fn):
        cmd = [self.python, '-X', 'utf8', '-I', os.path.join(tests_folder, script)] + script_args
        with open(log_fn, 'w', encoding='utf-8') as f:
            return subprocess.run(cmd, stdout=f, stderr=subprocess.STDOUT).returncode

    def run_cli(self, cli_args, log_fn):
        with open(log_fn, 'w', encoding='utf-8') as f:
            return subprocess.run([self.cli] + cli_args, stdout=f, stderr=subprocess.STDOUT).returncode

    def record(self, name, passed, message):
        if passed:
            self.n_passed += 1
        else:
            self.failures.append('{}: {}'.format(name, message))
            print('FAILED {}: {}'.format(name, message))

    def compare(self, name, folder, input_fn, options_fn):
        """
        Runs the Python reference and the C# tool on [input_fn], and compares outputs.
        """

        python_fn = os.path.join(folder, 'python.json')
        csharp_fn = os.path.join(folder, 'csharp.json')

        python_status = self.run_python(
            'python_reference.py',
            [input_fn, python_fn, options_fn or '-', os.path.join(folder, 'python_steps')],
            os.path.join(folder, 'python_log.txt'))
        cli_args = [input_fn, csharp_fn] + (['--options', options_fn] if options_fn else [])
        csharp_status = self.run_cli(cli_args, os.path.join(folder, 'csharp_log.txt'))

        if python_status != 0 or csharp_status != 0:
            self.record(name, False, 'python exit code {}, C# exit code {} (see logs in {})'.format(
                python_status, csharp_status, folder))
            return

        status, differences = compare_json_files(python_fn, csharp_fn, 5)
        self.record(name, status == 'identical',
                    status + ''.join('\n    ' + s for s in differences))


def main():

    parser = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    parser.add_argument('--working-folder', required=True,
                        help='folder for all test inputs and outputs (will be created)')
    parser.add_argument('--input', action='append', default=[],
                        help='a real results file to compare with each option set (can be repeated)')
    parser.add_argument('--cli', default=default_cli,
                        help='path to SpeciesNetPostprocessor.Cli.exe (default: Release build output)')
    parser.add_argument('--python', default=sys.executable,
                        help='Python interpreter with the megadetector package (default: this one)')
    parser.add_argument('--n-fuzz-cases', type=int, default=50)
    parser.add_argument('--seed', type=int, default=0)
    parser.add_argument('--skip-edge-cases', action='store_true')
    args = parser.parse_args()

    if not os.path.isfile(args.cli):
        print('Could not find the command-line tool at {}; build the solution in Release mode, or use '
              '--cli'.format(args.cli))
        return 1

    runner = Runner(args)
    start_time = time.time()
    os.makedirs(args.working_folder, exist_ok=True)

    # Real results files

    for i_input, input_fn in enumerate(args.input):

        input_name = '{}_{}'.format(i_input, os.path.splitext(os.path.basename(input_fn))[0])
        input_folder = os.path.join(args.working_folder, 'inputs', input_name)
        shutil.rmtree(input_folder, ignore_errors=True)
        os.makedirs(input_folder)
        print('Comparing results for {}'.format(input_fn))

        # JSON round-trip
        python_roundtrip_fn = os.path.join(input_folder, 'roundtrip_python.json')
        csharp_roundtrip_fn = os.path.join(input_folder, 'roundtrip_csharp.json')
        runner.run_python('roundtrip.py', [input_fn, python_roundtrip_fn],
                          os.path.join(input_folder, 'roundtrip_python_log.txt'))
        runner.run_cli(['--roundtrip', input_fn, csharp_roundtrip_fn],
                       os.path.join(input_folder, 'roundtrip_csharp_log.txt'))
        status, differences = compare_json_files(python_roundtrip_fn, csharp_roundtrip_fn, 5)
        runner.record('{}/roundtrip'.format(input_name), status == 'identical', status)

        normalized_fn = os.path.join(input_folder, 'normalized.json')
        runner.run_python('normalize_descriptions.py', [input_fn, normalized_fn],
                          os.path.join(input_folder, 'normalize_log.txt'))

        for option_set_name, options in OPTION_SETS.items():
            folder = os.path.join(input_folder, option_set_name)
            os.makedirs(folder)
            options_fn = os.path.join(folder, 'options.json')
            with open(options_fn, 'w') as f:
                json.dump(options, f, indent=1)
            runner.compare('{}/{}'.format(input_name, option_set_name), folder, normalized_fn, options_fn)

    # Random cases

    if args.n_fuzz_cases > 0:

        fuzz_folder = os.path.join(args.working_folder, 'fuzz_seed{}'.format(args.seed))
        shutil.rmtree(fuzz_folder, ignore_errors=True)
        print('Generating and comparing {} random cases in {}'.format(args.n_fuzz_cases, fuzz_folder))
        runner.run_python('generate_fuzz_cases.py', [fuzz_folder, str(args.n_fuzz_cases), str(args.seed)],
                          os.path.join(args.working_folder, 'generate_fuzz_cases_log.txt'))

        for case_name in sorted(os.listdir(fuzz_folder)):
            case_folder = os.path.join(fuzz_folder, case_name)
            runner.compare('fuzz_seed{}/{}'.format(args.seed, case_name), case_folder,
                           os.path.join(case_folder, 'input.json'),
                           os.path.join(case_folder, 'options.json'))

    # Edge cases

    if not args.skip_edge_cases:

        edge_folder = os.path.join(args.working_folder, 'edge_cases')
        shutil.rmtree(edge_folder, ignore_errors=True)
        os.makedirs(edge_folder)
        print('Checking edge cases in {}'.format(edge_folder))

        # A random case with timestamps for every image serves as the starting point
        base_folder = os.path.join(edge_folder, 'base')
        runner.run_python('generate_fuzz_cases.py', [base_folder, '1', str(args.seed), '--timestamp-mode', 'exif'],
                          os.path.join(edge_folder, 'generate_base_log.txt'))
        inputs_folder = os.path.join(edge_folder, 'inputs')
        make_edge_cases(os.path.join(base_folder, 'case_000', 'input.json'), inputs_folder)

        outputs_folder = os.path.join(edge_folder, 'outputs')
        os.makedirs(outputs_folder)
        for name, (expected_exit_code, expected_text) in EXPECTATIONS.items():
            log_fn = os.path.join(outputs_folder, name + '_log.txt')
            exit_code = runner.run_cli([os.path.join(inputs_folder, name + '.json'),
                                        os.path.join(outputs_folder, name + '.json')], log_fn)
            with open(log_fn, 'r', encoding='utf-8', errors='replace') as f:
                output = f.read()
            passed = (exit_code == expected_exit_code) and (expected_text in output)
            runner.record('edge_cases/{}'.format(name), passed,
                          'exit code {} (expected {}), expected output containing "{}" (see {})'.format(
                              exit_code, expected_exit_code, expected_text, log_fn))

    print('\n{} passed, {} failed ({:.0f} seconds)'.format(
        runner.n_passed, len(runner.failures), time.time() - start_time))
    for s in runner.failures:
        print('  ' + s)

    return 0 if len(runner.failures) == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
