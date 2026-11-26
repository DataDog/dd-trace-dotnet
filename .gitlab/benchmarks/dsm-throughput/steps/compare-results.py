#!/usr/bin/env python3

"""
compare-results.py
Compares two benchmark result JSON files and checks DSM overhead for duration and memory
Usage: python compare-results.py dsm-enabled.json tracer-no-dsm.json
"""

import json
import sys


def load_json_file(file_path):
    """Load and parse a JSON file."""
    try:
        with open(file_path, 'r') as f:
            return json.load(f)
    except FileNotFoundError:
        print(f"Error: File '{file_path}' not found")
        sys.exit(1)
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON in '{file_path}': {e}")
        sys.exit(1)


def extract_metrics(data):
    if not isinstance(data, list) or len(data) == 0:
        print("Error: Invalid benchmark data format - expected non-empty array")
        sys.exit(1)
    
    benchmark = data[0]
    
    # Extract median duration and stderr
    if 'median' not in benchmark:
        print("Error: No 'median' field found in benchmark data")
        sys.exit(1)

    if 'metrics' not in benchmark:
        print("Error: No 'metrics' field found in benchmark data")
        sys.exit(1)

    metrics = benchmark['metrics']

    median_duration = metrics['process.internal_duration_ms.median']
    if not isinstance(median_duration, (int, float)):
        print(f"Error: Invalid median duration value: {median_duration}")
        sys.exit(1)

    # Extract standard error
    stderr = metrics.get('process.internal_duration_ms.std_err', 0)
    if not isinstance(stderr, (int, float)):
        stderr = 0

    # Extract median memory
    memory_key = 'runtime.dotnet.mem.committed.median'
    if memory_key not in metrics:
        print(f"Error: No '{memory_key}' field found in metrics")
        sys.exit(1)

    median_memory = metrics[memory_key]
    if not isinstance(median_memory, (int, float)):
        print(f"Error: Invalid median memory value: {median_memory}")
        sys.exit(1)

    return median_duration, stderr, median_memory


def calculate_overhead(dsm_value, no_dsm_value):
    """Calculate the overhead percentage."""
    if no_dsm_value == 0:
        print("Error: No-DSM value is zero, cannot calculate overhead")
        sys.exit(1)
    
    overhead_pct = ((dsm_value - no_dsm_value) / no_dsm_value) * 100
    return overhead_pct

def format_duration_with_error(mean_ms, stderr_ms):
    """Format duration with error margin in milliseconds to a human-readable string."""
    if mean_ms >= 1000:  # >= 1 second
        mean_s = mean_ms / 1000
        stderr_s = stderr_ms / 1000
        return f"{mean_s:.3f} ± {stderr_s:.3f} s"
    else:
        return f"{mean_ms:.3f} ± {stderr_ms:.3f} ms"


def format_duration(ns):
    """Format duration in nanoseconds to a human-readable string."""
    if ns >= 1_000_000_000:  # >= 1 second
        return f"{ns / 1_000_000_000:.3f} s"
    elif ns >= 1_000_000:  # >= 1 ms
        return f"{ns / 1_000_000:.3f} ms"
    elif ns >= 1_000:  # >= 1 μs
        return f"{ns / 1_000:.3f} μs"
    else:
        return f"{ns:.0f} ns"


def format_memory(bytes_val):
    """Format memory in bytes to a human-readable string."""
    if bytes_val >= 1_000_000_000:  # >= 1 GB
        return f"{bytes_val / 1_000_000_000:.2f} GB"
    elif bytes_val >= 1_000_000:  # >= 1 MB
        return f"{bytes_val / 1_000_000:.2f} MB"
    elif bytes_val >= 1_000:  # >= 1 KB
        return f"{bytes_val / 1_000:.2f} KB"
    else:
        return f"{bytes_val:.0f} B"


def main():
    if len(sys.argv) != 3:
        print("Usage: python compare-results.py dsm-enabled.json tracer-no-dsm.json")
        sys.exit(1)
    
    dsm_file = sys.argv[1]
    no_dsm_file = sys.argv[2]
    threshold = 10.0
    
    # Load JSON files
    dsm_data = load_json_file(dsm_file)
    no_dsm_data = load_json_file(no_dsm_file)
    
    # Extract metrics (median-based for robustness against outliers)
    dsm_duration, dsm_stderr, dsm_memory = extract_metrics(dsm_data)
    no_dsm_duration, no_dsm_stderr, no_dsm_memory = extract_metrics(no_dsm_data)

    # Calculate overheads
    duration_overhead = calculate_overhead(dsm_duration, no_dsm_duration)
    memory_overhead = calculate_overhead(dsm_memory, no_dsm_memory)

    # Display results
    print(f"Duration (median) - DSM: {format_duration_with_error(dsm_duration, dsm_stderr)}")
    print(f"Duration (median) - No-DSM: {format_duration_with_error(no_dsm_duration, no_dsm_stderr)}")
    print(f"Memory (median) - DSM: {format_memory(dsm_memory)}, No-DSM: {format_memory(no_dsm_memory)}")
    print(f"Duration overhead: {duration_overhead:.2f}%")
    print(f"Memory overhead: {memory_overhead:.2f}%")
    
    # Check if overheads are within acceptable range
    duration_ok = duration_overhead <= threshold
    memory_ok = memory_overhead <= threshold
    
    if duration_ok and memory_ok:
        print(f"✅ PASS: Both overheads ≤ {threshold}%")
        return 0
    else:
        print(f"❌ FAIL: Overhead exceeds {threshold}%")
        if not duration_ok:
            print(f"  Duration: {duration_overhead:.2f}% > {threshold}%")
        if not memory_ok:
            print(f"  Memory: {memory_overhead:.2f}% > {threshold}%")
        print("Data Streams Monitoring overhead should be less than {threshold}% in this throughput scenario. Please investigate any regressions, or reach out to #data-streams-monitoring for help.")
        return 1


if __name__ == "__main__":
    sys.exit(main()) 