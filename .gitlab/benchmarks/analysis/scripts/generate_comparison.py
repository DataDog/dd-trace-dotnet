#!/usr/bin/env python3
"""
Generate comparison table and KDE plot for baseline vs custom benchmark config.
Follows the same style as plot_distributions.py.
"""

import csv
from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
from scipy.stats import gaussian_kde

ANALYSIS_DIR = Path(__file__).parent.parent
RESULTS_DIR = ANALYSIS_DIR / "results"

# Paths to summary CSVs
BASELINE_CSV = Path("/Users/augusto.deoliveira/go/src/github.com/DataDog/benchmarking-platform-tools/scripts/stability/data-dd-trace-dotnet-baseline/reports/summary.csv")
CUSTOM_CSV = Path("/Users/augusto.deoliveira/go/src/github.com/DataDog/benchmarking-platform-tools/scripts/stability/data-dd-trace-dotnet-2cpus10launchescustom/reports/summary.csv")

EXPERIMENTS = [
    ("baseline", "baseline"),
    ("custom", "new"),
]

COLORS = [
    "#1f77b4",  # blue - baseline (default matplotlib blue)
    "#ff7f0e",  # orange - custom (default matplotlib orange)
]


def load_summary_csv(filepath):
    """Load summary CSV and return dict keyed by metric."""
    data = {"execution_time": {"fp_rate": [], "max_bound": []},
            "throughput": {"fp_rate": [], "max_bound": []},
            "allocated_mem": {"fp_rate": [], "max_bound": []}}

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
                data[metric]["fp_rate"].append(fp_rate)
                data[metric]["max_bound"].append(max_bound)
            except (ValueError, KeyError):
                pass

    return data


def plot_kde(ax, values, color, label):
    """Plot a KDE (probability density function) for the given values."""
    values = np.array(values)
    if len(values) < 2:
        return

    # Remove outliers for better visualization (keep 99th percentile)
    p99 = np.percentile(values, 99)
    values_clipped = values[values <= p99]

    if len(values_clipped) < 2:
        values_clipped = values

    try:
        kde = gaussian_kde(values_clipped, bw_method=0.3)
        x_range = np.linspace(0, max(values_clipped) * 1.1, 200)
        ax.plot(x_range, kde(x_range), color=color, label=label, linewidth=2)
        ax.fill_between(x_range, kde(x_range), alpha=0.15, color=color)
    except Exception:
        # Fall back to histogram if KDE fails
        ax.hist(values, bins=20, density=True, alpha=0.3, color=color, label=label)


def create_comparison_figure():
    """Create a 2x3 figure comparing baseline vs custom for all metrics (transposed)."""
    # Load data
    baseline_data = load_summary_csv(BASELINE_CSV)
    custom_data = load_summary_csv(CUSTOM_CSV)

    all_data = {
        "baseline": baseline_data,
        "custom": custom_data,
    }

    metric_names = {
        "execution_time": "Execution Time",
        "throughput": "Throughput",
        "allocated_mem": "Allocated Memory (bytes/op)",
    }

    # Transposed: 2 rows (FP rate, Max CI diff) x 3 columns (metrics)
    fig, axes = plt.subplots(2, 3, figsize=(16, 8))
    fig.suptitle("Benchmark Stability Analysis: Baseline vs Custom Config",
                 fontsize=14, fontweight="bold")

    # Order: execution_time, throughput, allocated_mem
    for idx, metric in enumerate(["execution_time", "throughput", "allocated_mem"]):
        ax_fp = axes[0, idx]   # Top row: FP rate
        ax_max = axes[1, idx]  # Bottom row: Max CI diff

        # Plot FP rate distributions
        for (exp_key, exp_label), color in zip(EXPERIMENTS, COLORS):
            exp_data = all_data[exp_key][metric]
            fp_rates = exp_data["fp_rate"]
            max_bounds = exp_data["max_bound"]

            if fp_rates:
                avg_fp = np.mean(fp_rates)
                plot_kde(ax_fp, fp_rates, color, f"{exp_label} (avg={avg_fp:.1f}%)")

            if max_bounds:
                # Filter out zeros for max bound visualization
                max_bounds_filtered = [m for m in max_bounds if m > 0.1]
                if max_bounds_filtered:
                    plot_kde(ax_max, max_bounds_filtered, color, exp_label)

        # Style FP rate plot
        ax_fp.set_xlabel("FP Rate (%)", fontsize=11)
        ax_fp.set_ylabel("Density", fontsize=11)
        ax_fp.set_title(f"FP Rate Distribution\n({metric_names[metric]})", fontsize=12)
        ax_fp.legend(loc="upper right", fontsize=9)
        ax_fp.set_xlim(left=0)
        ax_fp.grid(True, alpha=0.3)

        # Style max bound plot
        ax_max.set_xlabel("Max CI Diff (%)", fontsize=11)
        ax_max.set_ylabel("Density", fontsize=11)
        ax_max.set_title(f"Max CI Diff Distribution\n({metric_names[metric]})", fontsize=12)
        ax_max.legend(loc="upper right", fontsize=9)
        ax_max.set_xlim(left=0)
        ax_max.grid(True, alpha=0.3)

    plt.tight_layout()
    return fig


def create_comparison_table():
    """Create comparison table between baseline and custom config."""
    baseline_data = load_summary_csv(BASELINE_CSV)
    custom_data = load_summary_csv(CUSTOM_CSV)

    # Load full data with scenario info
    rows = []

    with open(BASELINE_CSV, newline="") as f:
        reader = csv.DictReader(f)
        baseline_rows = {(r["scenario"], r["metric"]): r for r in reader
                        if "FP Rate" not in r.get("scenario", "") and "Bound" not in r.get("scenario", "")}

    with open(CUSTOM_CSV, newline="") as f:
        reader = csv.DictReader(f)
        custom_rows = {(r["scenario"], r["metric"]): r for r in reader
                      if "FP Rate" not in r.get("scenario", "") and "Bound" not in r.get("scenario", "")}

    all_keys = set(baseline_rows.keys()) | set(custom_rows.keys())

    for scenario, metric in sorted(all_keys):
        baseline = baseline_rows.get((scenario, metric), {})
        custom = custom_rows.get((scenario, metric), {})

        benchmark = scenario.replace("Benchmarks.Trace.", "")

        try:
            baseline_fp = float(baseline.get("significant_impact_fp_rate", 0))
            baseline_max = float(baseline.get("max_significant_impact_bound", 0))
        except (ValueError, TypeError):
            baseline_fp, baseline_max = 0, 0

        try:
            custom_fp = float(custom.get("significant_impact_fp_rate", 0))
            custom_max = float(custom.get("max_significant_impact_bound", 0))
        except (ValueError, TypeError):
            custom_fp, custom_max = 0, 0

        rows.append({
            "Benchmark": benchmark,
            "Metric": metric,
            "Baseline FP%": round(baseline_fp, 1),
            "Baseline Max%": round(baseline_max, 1),
            "Custom FP%": round(custom_fp, 1),
            "Custom Max%": round(custom_max, 1),
        })

    # Sort by Custom FP% descending
    rows.sort(key=lambda x: (-x["Custom FP%"], -x["Baseline FP%"]))

    return rows


def main():
    # Generate comparison table
    print("Generating comparison table...")
    rows = create_comparison_table()

    output_csv = RESULTS_DIR / "comparison_baseline_vs_custom.csv"
    with open(output_csv, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=["Benchmark", "Metric", "Baseline FP%", "Baseline Max%", "Custom FP%", "Custom Max%"])
        writer.writeheader()
        writer.writerows(rows)
    print(f"Saved to {output_csv}")

    # Print summary stats
    baseline_data = load_summary_csv(BASELINE_CSV)
    custom_data = load_summary_csv(CUSTOM_CSV)

    all_baseline_fp = []
    all_custom_fp = []
    all_baseline_max = []
    all_custom_max = []

    for metric in ["execution_time", "throughput", "allocated_mem"]:
        all_baseline_fp.extend(baseline_data[metric]["fp_rate"])
        all_custom_fp.extend(custom_data[metric]["fp_rate"])
        all_baseline_max.extend(baseline_data[metric]["max_bound"])
        all_custom_max.extend(custom_data[metric]["max_bound"])

    print("\n=== Summary Statistics ===")
    print(f"Baseline FP Rate: {np.mean(all_baseline_fp):.2f}%")
    print(f"Custom FP Rate:   {np.mean(all_custom_fp):.2f}%")
    print(f"Baseline Max Bound: {max(all_baseline_max):.2f}%")
    print(f"Custom Max Bound:   {max(all_custom_max):.2f}%")

    # Generate KDE plot
    print("\nGenerating KDE plot...")
    fig = create_comparison_figure()
    output_png = RESULTS_DIR / "kde_baseline_vs_custom.png"
    fig.savefig(output_png, dpi=150, bbox_inches="tight")
    print(f"Saved to {output_png}")
    plt.close()


if __name__ == "__main__":
    main()
