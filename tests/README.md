# Tests

The scripts in this folder check that SpeciesNet Postprocessor produces the same output as the [MegaDetector Python code](https://github.com/agentmorris/MegaDetector) it was ported from, using the program's command-line tool (SpeciesNetPostprocessor.Cli), which uses exactly the same processing code as the GUI.


## Comparison tests

These need a Python environment with the MegaDetector package (pip install megadetector), and a Release build of the solution (see "Building from source" in the main README). Run:

`python tests/run_comparisons.py --working-folder c:\temp\speciesnet-postprocessor-tests --input c:\data\results.json`

All test inputs and outputs are written to the working folder, which should be outside the repo. The script:

* For each file passed with --input (which can be repeated), checks that reading and re-writing the file matches MegaDetector's write_json(), then runs the Python code and the C# command-line tool with each of several option sets (see OPTION_SETS in run_comparisons.py), and checks that the outputs are byte-for-byte identical. Inputs are first passed through normalize_descriptions.py, so files the Python code can't process (e.g. AddaxAI output, which is missing some category descriptions) can still be compared.
* Generates random results files (--n-fuzz-cases, default 50, with --seed controlling the random seed), each with random options, and compares outputs the same way. These exercise things like ties, related taxa, "other" categories, merged categories, non-animal classifications, failed images, camera overflow folders, images exactly 60 seconds apart, and invalid timestamps.
* Generates edge cases that the Python code can't process (e.g. unsorted classifications, missing timestamps, duplicate filenames, truncated files), and checks the exit code and output of the C# tool for each.

The script prints a summary and exits with 0 if everything passed. Use --cli if the command-line tool isn't in the default Release build location, and --python to use a different Python environment for the MegaDetector code. If a comparison fails, its folder contains both outputs (python.json and csharp.json), both logs, and the Python code's results after each step (in python_steps); to get the C# results after each step, run SpeciesNetPostprocessor.Cli with --intermediate-folder.

Real results files are the most valuable inputs; a few thousand images from an AddaxAI run and from a MegaDetector/SpeciesNet run (with --include_exif_tags DateTimeOriginal, so there are timestamps) are good choices. make_subset.py extracts the images from the first few folders of a large results file. Large files work too, but the Python code takes about 5 minutes to process a 300,000-image file with each option set.


## GUI smoke test

gui/drive_gui.ps1 runs the program (e.g. a published .exe), fills in the input and output files, clicks "Process", waits for processing to finish, and saves screenshots along the way. It can also open the advanced options dialog (-Advanced) or test cancellation (-CancelAfterSeconds). The program's saved settings are backed up before the test and restored afterwards. For example:

`powershell -File tests\gui\drive_gui.ps1 -Exe publish\SpeciesNetPostprocessor.exe -InputFile c:\data\results.json -OutputFile c:\temp\results_gui.json -ScreenshotFolder c:\temp\screenshots -Advanced`


## Scripts

* **run_comparisons.py**: runs all of the comparison tests, as described above.
* **python_reference.py**: runs the MegaDetector Python code with the same steps and options as the C# program, using the same options file format as the command-line tool.
* **compare_json.py**: compares two .json files, byte-for-byte and then semantically.
* **normalize_descriptions.py**: fills in missing classification category descriptions, and standardizes the human/"no cv result" descriptions, so the Python code can process a file.
* **generate_fuzz_cases.py**: generates random results files and options files.
* **make_edge_cases.py**: generates edge-case inputs, and defines the expected outcome for each.
* **roundtrip.py**: reads and re-writes a .json file with MegaDetector's write_json().
* **make_subset.py**: extracts the images from the first N folders of a results file.
* **gui/drive_gui.ps1**, **gui/screenshot_window.ps1**: the GUI smoke test.
