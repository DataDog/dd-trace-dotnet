#!/usr/bin/env python3
"""
Build experiments comparison table from all stability experiment data.
"""

import csv
from pathlib import Path
import numpy as np

RESULTS_DIR = Path(__file__).parent.parent / "results"
DATA_BASE = Path("/Users/augusto.deoliveira/go/src/github.com/DataDog/benchmarking-platform-tools/scripts/stability")

# Experiments with their display names
# Format: (directory_name, display_name)
EXPERIMENTS = [
    ("data-dd-trace-dotnet-baseline", "baseline (seq, all CPUs, autopilot)"),
    ("data-dd-trace-dotnet-1cpu10launches100ms", "1cpu-10L-100ms-autopilot"),
    ("data-dd-trace-dotnet-2cpus10launches", "2cpus-10L-200ms-autopilot"),
    ("data-dd-trace-dotnet-2cpus20launches", "2cpus-20L-200ms-autopilot"),
    ("data-dd-trace-dotnet-2cpus10launches500ms", "2cpus-10L-500ms-autopilot"),
    ("data-dd-trace-dotnet-2cpus5launches500ms", "2cpus-5L-500ms-autopilot"),
    ("data-dd-trace-dotnet-2cpus10launches200msfixedcounts", "2cpus-10L-200ms-20W10I"),
    ("data-dd-trace-dotnet-2cpus10launches500msfixedcounts", "2cpus-10L-500ms-20W10I"),
    ("data-dd-trace-dotnet-2cpus5launches500msfixedcounts", "2cpus-5L-500ms-20W10I"),
    ("data-dd-trace-dotnet-2cpus10launchescustom", "2cpus-10L-stable200ms/flaky500ms-10W10I"),
]


def load_summary_csv(filepath):
    """Load summary CSV and return aggregated stats by metric."""
    data = {"execution_time": {"fp_rates": [], "max_bounds": []},
            "throughput": {"fp_rates": [], "max_bounds": []},
            "allocated_mem": {"fp_rates": [], "max_bounds": []}}

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
            if metric not in data:
                continue

            try:
                fp_rate = float(row["significant_impact_fp_rate"])
                max_bound = float(row["max_significant_impact_bound"])
                data[metric]["fp_rates"].append(fp_rate)
                data[metric]["max_bounds"].append(max_bound)
            except (ValueError, KeyError):
                pass

    return data


def compute_stats(values):
    """Compute mean and max for a list of values."""
    if not values:
        return None, None
    return np.mean(values), np.max(values)


def main():
    rows = []

    for dir_name, display_name in EXPERIMENTS:
        summary_path = DATA_BASE / dir_name / "reports" / "summary.csv"
        data = load_summary_csv(summary_path)

        if data is None:
            print(f"Warning: No data for {dir_name}")
            continue

        row = {"experiment": display_name}

        for metric in ["execution_time", "throughput", "allocated_mem"]:
            fp_rates = data[metric]["fp_rates"]
            max_bounds = data[metric]["max_bounds"]

            avg_fp, _ = compute_stats(fp_rates)
            _, max_bound = compute_stats(max_bounds)

            prefix = metric.replace("_", " ").title().replace(" ", "")
            if metric == "execution_time":
                prefix = "ExecTime"
            elif metric == "throughput":
                prefix = "Throughput"
            elif metric == "allocated_mem":
                prefix = "AllocMem"

            row[f"{prefix}_AvgFP%"] = round(avg_fp, 2) if avg_fp is not None else ""
            row[f"{prefix}_MaxCI%"] = round(max_bound, 2) if max_bound is not None else ""

        # Also compute overall stats
        all_fp = []
        all_max = []
        for metric in ["execution_time", "throughput", "allocated_mem"]:
            all_fp.extend(data[metric]["fp_rates"])
            all_max.extend(data[metric]["max_bounds"])

        avg_fp, _ = compute_stats(all_fp)
        _, max_bound = compute_stats(all_max)
        row["Overall_AvgFP%"] = round(avg_fp, 2) if avg_fp is not None else ""
        row["Overall_MaxCI%"] = round(max_bound, 2) if max_bound is not None else ""

        rows.append(row)

    # Write CSV
    fieldnames = [
        "experiment",
        "ExecTime_AvgFP%", "ExecTime_MaxCI%",
        "Throughput_AvgFP%", "Throughput_MaxCI%",
        "AllocMem_AvgFP%", "AllocMem_MaxCI%",
        "Overall_AvgFP%", "Overall_MaxCI%",
    ]

    output_path = RESULTS_DIR / "experiments_comparison.csv"
    with open(output_path, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    print(f"Saved to {output_path}")

    # Print summary table
    print("\n=== Experiments Comparison ===\n")
    print(f"{'Experiment':<45} {'ExecTime':<12} {'Throughput':<12} {'AllocMem':<12} {'Overall':<12}")
    print(f"{'':45} {'AvgFP%':<6}{'MaxCI%':<6} {'AvgFP%':<6}{'MaxCI%':<6} {'AvgFP%':<6}{'MaxCI%':<6} {'AvgFP%':<6}{'MaxCI%':<6}")
    print("-" * 93)
    for row in rows:
        print(f"{row['experiment']:<45} "
              f"{row.get('ExecTime_AvgFP%', ''):<6}{row.get('ExecTime_MaxCI%', ''):<6} "
              f"{row.get('Throughput_AvgFP%', ''):<6}{row.get('Throughput_MaxCI%', ''):<6} "
              f"{row.get('AllocMem_AvgFP%', ''):<6}{row.get('AllocMem_MaxCI%', ''):<6} "
              f"{row.get('Overall_AvgFP%', ''):<6}{row.get('Overall_MaxCI%', ''):<6}")


if __name__ == "__main__":
    main()
