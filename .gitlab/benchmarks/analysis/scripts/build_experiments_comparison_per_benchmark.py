#!/usr/bin/env python3
"""
Build experiments comparison table per benchmark from all stability experiment data.
"""

import csv
from pathlib import Path
from collections import defaultdict

RESULTS_DIR = Path(__file__).parent.parent / "results"
DATA_BASE = Path("/Users/augusto.deoliveira/go/src/github.com/DataDog/benchmarking-platform-tools/scripts/stability")

# Experiments with their display names (short names for columns)
EXPERIMENTS = [
    ("data-dd-trace-dotnet-baseline", "baseline"),
    ("data-dd-trace-dotnet-1cpu10launches100ms", "1cpu-10L-100ms"),
    ("data-dd-trace-dotnet-2cpus10launches", "2cpus-10L-200ms"),
    ("data-dd-trace-dotnet-2cpus20launches", "2cpus-20L-200ms"),
    ("data-dd-trace-dotnet-2cpus10launches500ms", "2cpus-10L-500ms"),
    ("data-dd-trace-dotnet-2cpus5launches500ms", "2cpus-5L-500ms"),
    ("data-dd-trace-dotnet-2cpus10launches200msfixedcounts", "2cpus-10L-200ms-20W10I"),
    ("data-dd-trace-dotnet-2cpus10launches500msfixedcounts", "2cpus-10L-500ms-20W10I"),
    ("data-dd-trace-dotnet-2cpus5launches500msfixedcounts", "2cpus-5L-500ms-20W10I"),
    ("data-dd-trace-dotnet-2cpus10launchescustom", "custom-stable200ms/flaky500ms-10W10I"),
]


def load_summary_csv(filepath):
    """Load summary CSV and return dict keyed by (scenario, metric)."""
    data = {}

    if not filepath.exists():
        return None

    with open(filepath, newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            scenario = row.get("scenario", "")
            metric = row.get("metric", "")

            # Skip aggregate rows
            if "FP Rate" in scenario or "Bound" in scenario:
                continue

            try:
                fp_rate = float(row["significant_impact_fp_rate"])
                max_bound = float(row["max_significant_impact_bound"])

                # Simplify scenario name
                scenario_short = scenario.replace("Benchmarks.Trace.", "")

                data[(scenario_short, metric)] = {
                    "fp_rate": fp_rate,
                    "max_bound": max_bound,
                }
            except (ValueError, KeyError):
                pass

    return data


def main():
    # Load all experiment data
    all_data = {}
    for dir_name, display_name in EXPERIMENTS:
        summary_path = DATA_BASE / dir_name / "reports" / "summary.csv"
        data = load_summary_csv(summary_path)
        if data:
            all_data[display_name] = data
        else:
            print(f"Warning: No data for {dir_name}")

    # Collect all unique (scenario, metric) pairs
    all_keys = set()
    for exp_data in all_data.values():
        all_keys.update(exp_data.keys())

    # Sort by scenario then metric
    all_keys = sorted(all_keys, key=lambda x: (x[0], x[1]))

    # Build rows
    rows = []
    for scenario, metric in all_keys:
        row = {
            "benchmark": scenario,
            "metric": metric,
        }

        for _, display_name in EXPERIMENTS:
            exp_data = all_data.get(display_name, {})
            key_data = exp_data.get((scenario, metric), {})

            fp_rate = key_data.get("fp_rate")
            max_bound = key_data.get("max_bound")

            row[f"{display_name}_FP%"] = round(fp_rate, 1) if fp_rate is not None else ""
            row[f"{display_name}_MaxCI%"] = round(max_bound, 1) if max_bound is not None else ""

        rows.append(row)

    # Build fieldnames
    fieldnames = ["benchmark", "metric"]
    for _, display_name in EXPERIMENTS:
        fieldnames.append(f"{display_name}_FP%")
        fieldnames.append(f"{display_name}_MaxCI%")

    # Write CSV
    output_path = RESULTS_DIR / "experiments_comparison_per_benchmark.csv"
    with open(output_path, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print(f"Saved to {output_path}")
    print(f"Total rows: {len(rows)}")
    print(f"Experiments: {len(EXPERIMENTS)}")


if __name__ == "__main__":
    main()
