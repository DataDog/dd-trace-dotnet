#!/usr/bin/env python3
"""
Combines benchmark stability experiment results into a single comparison CSV.

Output format:
benchmark | <experiment>_fp_rate | <experiment>_max_bound | ...
"""

import csv
import os
from pathlib import Path

ANALYSIS_DIR = Path(__file__).parent

EXPERIMENTS = {
    "baseline.csv": "baseline_seq",
    "old_baseline.csv": "old_baseline",
    "1cpu-10launches-100ms.csv": "1cpu_10l_100ms",
    "2cpus-10launches-100ms.csv": "2cpu_10l_100ms",
    "2cpus-20launches-100ms.csv": "2cpu_20l_100ms",
    "2cpus-10launches-200ms.csv": "2cpu_10l_200ms",
    "2cpus-10launches-500ms.csv": "2cpu_10l_500ms",
    "2cpus-5launches-500ms.csv": "2cpu_5l_500ms",
    "2cpu_10l_500ms_fixed.csv": "2cpu_10l_500ms_fixed",
    "2cpu_10l_200ms_fixed.csv": "2cpu_10l_200ms_fixed",
    "2cpu_5l_500ms_fixed.csv": "2cpu_5l_500ms_fixed",
    "2cpu_10l_custom.csv": "2cpu_10l_10W10I_200_500ms",
}


def load_experiment(filepath: Path) -> dict:
    """Load experiment CSV and return dict keyed by (scenario, metric)."""
    data = {}
    with open(filepath, newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            # Skip empty rows
            if not row.get("scenario") or not row.get("metric"):
                continue
            if not row.get("significant_impact_fp_rate"):
                continue
            key = (row["scenario"], row["metric"])
            data[key] = {
                "fp_rate": float(row["significant_impact_fp_rate"]),
                "max_bound": float(row["max_significant_impact_bound"]),
            }
    return data


def main():
    # Load all experiments
    experiments = {}
    all_benchmarks = set()

    for filename, exp_name in EXPERIMENTS.items():
        filepath = ANALYSIS_DIR / filename
        if filepath.exists():
            experiments[exp_name] = load_experiment(filepath)
            all_benchmarks.update(experiments[exp_name].keys())
            print(f"Loaded {exp_name}: {len(experiments[exp_name])} benchmarks")
        else:
            print(f"Warning: {filepath} not found")

    # Sort benchmarks for consistent output
    all_benchmarks = sorted(all_benchmarks)

    # Build header
    header = ["benchmark", "metric"]
    for exp_name in EXPERIMENTS.values():
        if exp_name in experiments:
            header.append(f"{exp_name}_fp_rate")
            header.append(f"{exp_name}_max_bound")

    # Build rows
    rows = []
    for scenario, metric in all_benchmarks:
        row = [scenario, metric]
        for exp_name in EXPERIMENTS.values():
            if exp_name in experiments:
                exp_data = experiments[exp_name].get((scenario, metric))
                if exp_data:
                    row.append(f"{exp_data['fp_rate']:.2f}")
                    row.append(f"{exp_data['max_bound']:.2f}")
                else:
                    row.append("")
                    row.append("")
        rows.append(row)

    # Write output
    output_path = ANALYSIS_DIR / "experiments_comparison.csv"
    with open(output_path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(header)
        writer.writerows(rows)

    print(f"\nWrote {len(rows)} benchmarks to {output_path}")


if __name__ == "__main__":
    main()
