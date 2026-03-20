#!/usr/bin/env python3
"""
Plot PDF distributions for FP rate and max bound across experiments.

Creates figures for execution_time, throughput, and allocated_mem, each with two subplots:
- Left: FP rate distribution (KDE)
- Right: Max bound distribution (KDE)
"""

import csv
from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
from scipy.stats import gaussian_kde

ANALYSIS_DIR = Path(__file__).parent

# Full comparison (all experiments)
EXPERIMENTS_FULL = [
    ("baseline_seq", "Baseline (all CPUs, 1L, 200ms, autopilot)"),
    ("2cpu_10l_200ms", "2 CPU, 10L, 200ms, autopilot"),
    ("2cpu_10l_500ms", "2 CPU, 10L, 500ms, autopilot"),
    ("2cpu_10l_500ms_fixed", "2 CPU, 10L, 500ms, no autopilot"),
    ("2cpu_10l_200ms_fixed", "2 CPU, 10L, 200ms, no autopilot"),
]

# Simple comparison (baseline vs 500ms no autopilot)
EXPERIMENTS_SIMPLE = [
    ("baseline_seq", "Baseline (all CPUs, 1L, 200ms, autopilot)"),
    ("2cpu_10l_500ms_fixed", "2 CPU, 10L, 500ms, no autopilot"),
]

# Three-way comparison for stable benchmarks analysis
EXPERIMENTS_THREE_WAY = [
    ("baseline_seq", "Baseline (all CPUs, 1L, 200ms, autopilot)"),
    ("2cpu_10l_200ms_fixed", "2 CPU, 10L, 200ms, no autopilot"),
    ("2cpu_10l_500ms_fixed", "2 CPU, 10L, 500ms, no autopilot"),
]

# Launches comparison (5L vs 10L)
EXPERIMENTS_LAUNCHES = [
    ("baseline_seq", "Baseline (all CPUs, 1L, 200ms, autopilot)"),
    ("2cpu_5l_500ms_fixed", "2 CPU, 5L, 500ms, no autopilot"),
    ("2cpu_10l_500ms_fixed", "2 CPU, 10L, 500ms, no autopilot"),
]

# Known flaky benchmarks (FP rate > 10% in any config)
KNOWN_FLAKY_BENCHMARKS = {
    "Datadog.Trace.Benchmarks.AgentWriterBenchmark.WriteAndFlushEnrichedTraces",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.AllCycleSimulatedBodyIp",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.NoSecurityEmptyContext",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.NoSecuritySimulatedBodyIp",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.OneAddressOneRuleEmptyContext",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.OneAddressOneRuleMatch",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.OneAddressOneRuleNoMatch",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.OneAddressOneRuleNoMatchSimulatedBody",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.OneAddressOneRuleNoMatchSimulatedBodyIp",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.SecurityNoRulesEmptyContext",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.SecurityNoRulesSimulatedBodyIp",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.WafWithAttackIp",
    "Datadog.Trace.Benchmarks.AppSecBodyBenchmark.WafWithAttackWithBody",
    "Datadog.Trace.Benchmarks.AppSecEncoderBenchmark.EncodeArgs",
    "Datadog.Trace.Benchmarks.AppSecWafBenchmark.RunWafRealisticBenchmark",
    "Datadog.Trace.Benchmarks.AppSecWafBenchmark.RunWafRealisticBenchmarkWithAttack",
    "Datadog.Trace.Benchmarks.AspNetCoreBenchmark.SendRequest",
    "Datadog.Trace.Benchmarks.DatadogLoggingBenchmark.LogsInjectionEnabled",
    "Datadog.Trace.Benchmarks.DbCommandBenchmark.ExecuteNonQuery",
    "Datadog.Trace.Benchmarks.DuckTypeExplicitInterfaceBenchmark.DuckTypeFieldFromValueType",
    "Datadog.Trace.Benchmarks.DuckTypeExplicitInterfaceBenchmark.DuckTypePropertyFromValueType",
    "Datadog.Trace.Benchmarks.DuckTypeValueTypeFieldBenchmark.GetProxyFromStruct",
    "Datadog.Trace.Benchmarks.DuckTypeValueTypeFieldBenchmark.GetProxyFromStructMethod",
    "Datadog.Trace.Benchmarks.DuckTypeValueTypePropertyBenchmark.GetProxyFromStruct",
    "Datadog.Trace.Benchmarks.DuckTypeValueTypePropertyBenchmark.GetProxyFromStructMethod",
    "Datadog.Trace.Benchmarks.HttpClientBenchmark.SendAsync",
    "Datadog.Trace.Benchmarks.IastBenchmark.RequestReturnsAspect",
    "Datadog.Trace.Benchmarks.IastBenchmark.RequestReturnsNothing",
    "Datadog.Trace.Benchmarks.IastBenchmark.RequestReturnsVulnerability",
    "Datadog.Trace.Benchmarks.LogBenchmark.EnrichedLog",
    "Datadog.Trace.Benchmarks.SpanBenchmark.SetTagInt",
    "Datadog.Trace.Benchmarks.StringBuilderCacheBenchmark.AcquireReleaseNotPooled",
    "Datadog.Trace.Benchmarks.StringBuilderCacheBenchmark.AcquireReleasePooled",
    "Datadog.Trace.Benchmarks.TracerBenchmark.StartFinishScope",
    "Datadog.Trace.Benchmarks.TracerBenchmark.StartFinishSpan",
}

COLORS_FULL = [
    "#1f77b4",  # blue - baseline
    "#9467bd",  # purple - 200ms autopilot
    "#8c564b",  # brown - 500ms autopilot
    "#17becf",  # cyan - 500ms no autopilot
    "#2ca02c",  # green - 200ms no autopilot
]

COLORS_SIMPLE = [
    "#1f77b4",  # blue - baseline
    "#17becf",  # cyan - 500ms no autopilot
]

COLORS_THREE_WAY = [
    "#1f77b4",  # blue - baseline
    "#2ca02c",  # green - 200ms no autopilot
    "#17becf",  # cyan - 500ms no autopilot
]

COLORS_LAUNCHES = [
    "#1f77b4",  # blue - baseline
    "#ff7f0e",  # orange - 5L
    "#17becf",  # cyan - 10L
]


def load_comparison_csv(experiments, exclude_flaky=False):
    """Load the experiments comparison CSV."""
    data = {"execution_time": {}, "throughput": {}, "allocated_mem": {}}

    for exp_key, _ in experiments:
        data["execution_time"][exp_key] = {"fp_rate": [], "max_bound": []}
        data["throughput"][exp_key] = {"fp_rate": [], "max_bound": []}
        data["allocated_mem"][exp_key] = {"fp_rate": [], "max_bound": []}

    csv_path = ANALYSIS_DIR / "experiments_comparison.csv"
    with open(csv_path, newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            benchmark = row["benchmark"]
            metric = row["metric"]
            if metric not in data:
                continue

            # Skip known flaky benchmarks if requested
            if exclude_flaky and benchmark in KNOWN_FLAKY_BENCHMARKS:
                continue

            for exp_key, _ in experiments:
                fp_col = f"{exp_key}_fp_rate"
                max_col = f"{exp_key}_max_bound"

                if row.get(fp_col) and row.get(max_col):
                    try:
                        fp_rate = float(row[fp_col])
                        max_bound = float(row[max_col])
                        data[metric][exp_key]["fp_rate"].append(fp_rate)
                        data[metric][exp_key]["max_bound"].append(max_bound)
                    except ValueError:
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


def create_figure(metric_data, metric_name, output_path, experiments, colors):
    """Create a figure with two subplots for FP rate and max bound."""
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(16, 6))

    for (exp_key, exp_label), color in zip(experiments, colors):
        exp_data = metric_data.get(exp_key, {})

        fp_rates = exp_data.get("fp_rate", [])
        max_bounds = exp_data.get("max_bound", [])

        if fp_rates:
            plot_kde(ax1, fp_rates, color, exp_label)
        if max_bounds:
            plot_kde(ax2, max_bounds, color, exp_label)

    ax1.set_xlabel("FP Rate (%)", fontsize=12)
    ax1.set_ylabel("Density", fontsize=12)
    ax1.set_title(f"Significant Impact FP Rate Distribution\n({metric_name})", fontsize=14)
    ax1.legend(loc="upper right", fontsize=9)
    ax1.set_xlim(left=0)
    ax1.grid(True, alpha=0.3)

    ax2.set_xlabel("Max Bound (%)", fontsize=12)
    ax2.set_ylabel("Density", fontsize=12)
    ax2.set_title(f"Max Significant Impact Bound Distribution\n({metric_name})", fontsize=14)
    ax2.legend(loc="upper right", fontsize=9)
    ax2.set_xlim(left=0)
    ax2.grid(True, alpha=0.3)

    fig.suptitle(f"Benchmark Stability Analysis: {metric_name}", fontsize=16, fontweight="bold")
    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches="tight")
    print(f"Saved {output_path}")
    plt.close()


def main():
    # Full comparison (all experiments)
    print("Generating full comparison plots...")
    data_full = load_comparison_csv(EXPERIMENTS_FULL)

    for metric, metric_name in [
        ("execution_time", "Execution Time"),
        ("throughput", "Throughput"),
        ("allocated_mem", "Allocated Memory"),
    ]:
        create_figure(
            data_full[metric],
            metric_name,
            ANALYSIS_DIR / f"distribution_{metric}.png",
            EXPERIMENTS_FULL,
            COLORS_FULL,
        )

    # Simple comparison (baseline vs 500ms no autopilot)
    print("\nGenerating simple comparison plots (baseline vs 500ms no autopilot)...")
    data_simple = load_comparison_csv(EXPERIMENTS_SIMPLE)

    for metric, metric_name in [
        ("execution_time", "Execution Time"),
        ("throughput", "Throughput"),
        ("allocated_mem", "Allocated Memory"),
    ]:
        create_figure(
            data_simple[metric],
            metric_name,
            ANALYSIS_DIR / f"distribution_{metric}_simple.png",
            EXPERIMENTS_SIMPLE,
            COLORS_SIMPLE,
        )

    # Three-way comparison excluding flaky benchmarks (stable benchmarks only)
    print("\nGenerating stable benchmark plots (excluding known flaky benchmarks)...")
    data_stable = load_comparison_csv(EXPERIMENTS_THREE_WAY, exclude_flaky=True)

    for metric, metric_name in [
        ("execution_time", "Execution Time"),
        ("throughput", "Throughput"),
        ("allocated_mem", "Allocated Memory"),
    ]:
        create_figure(
            data_stable[metric],
            f"{metric_name} (Stable Benchmarks Only)",
            ANALYSIS_DIR / f"distribution_{metric}_stable.png",
            EXPERIMENTS_THREE_WAY,
            COLORS_THREE_WAY,
        )

    # Launches comparison (5L vs 10L)
    print("\nGenerating launches comparison plots (5L vs 10L)...")
    data_launches = load_comparison_csv(EXPERIMENTS_LAUNCHES)

    for metric, metric_name in [
        ("execution_time", "Execution Time"),
        ("throughput", "Throughput"),
        ("allocated_mem", "Allocated Memory"),
    ]:
        create_figure(
            data_launches[metric],
            f"{metric_name} (5L vs 10L)",
            ANALYSIS_DIR / f"distribution_{metric}_launches.png",
            EXPERIMENTS_LAUNCHES,
            COLORS_LAUNCHES,
        )

    print("\nDone!")


if __name__ == "__main__":
    main()
