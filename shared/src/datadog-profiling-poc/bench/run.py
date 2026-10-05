#!/usr/bin/env python3
"""Runs the PoC vs libdatadog benchmark matrix and writes a Markdown report.

Usage: run.py --poc <bench_poc binary> --ldd <bench_ldd binary> --out <dir>
              [--scenarios a,b,...] [--repeat N] [--cpu N] [--ldd-version vX.Y.Z] [--quick]

Both binaries are built from bench.c (see run-benchmarks.sh / .ps1). Only the
Python standard library is used, so this runs on Linux and Windows alike.
"""
import argparse
import datetime
import http.server
import json
import os
import platform
import socketserver
import statistics
import subprocess
import sys
import threading

# name: (bench arguments, description)
SCENARIOS = {
    "scale_1k": (["--samples", "1000"], "1k samples, 1000 stacks of depth 32, timestamps"),
    "scale_10k": (["--samples", "10000"], "10k samples, 1000 stacks of depth 32, timestamps"),
    "no_ts_10k": (["--samples", "10000", "--ts", "0"], "10k samples without timestamps (few duplicates)"),
    "aggregation_heavy_20k": (["--samples", "20000", "--ts", "0", "--labels", "0", "--stacks", "50"],
                              "20k samples without timestamps, ~400 distinct (stack, labels)"),
    "shallow_100k": (["--samples", "100000", "--depth", "8", "--stacks", "200"], "100k samples, 200 stacks of depth 8"),
    "endpoints_upscale_5k": (["--samples", "5000", "--endpoints", "1000", "--upscale", "1"],
                             "5k samples + 1000 endpoints + 32 upscaling rules"),
}
EXPORT_ARGS = ["--samples", "2000", "--depth", "8", "--stacks", "200", "--sends", "20", "--file-kb", "16"]
SAMPLES = {name: int(args[args.index("--samples") + 1]) for name, (args, _) in SCENARIOS.items()}
IS_WINDOWS = os.name == "nt"


class Sink(http.server.BaseHTTPRequestHandler):
    """Accepts uploads like the agent does: reads the body, answers 202, honours Connection: close."""
    protocol_version = "HTTP/1.1"

    def do_POST(self):
        if self.headers.get("Content-Length"):
            self.rfile.read(int(self.headers["Content-Length"]))
        else:  # chunked
            while True:
                n = int(self.rfile.readline().split(b";")[0], 16)
                self.rfile.read(n + 2)
                if n == 0:
                    break
        self.send_response(202)
        self.send_header("Content-Length", "0")
        self.send_header("Connection", "close")
        self.end_headers()
        self.close_connection = True

    def log_message(self, *args):
        pass


def start_sink():
    class Server(socketserver.ThreadingMixIn, http.server.HTTPServer):
        daemon_threads = True
        allow_reuse_address = True

    server = Server(("127.0.0.1", 0), Sink)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server, f"http://127.0.0.1:{server.server_address[1]}"


def run(binary, args):
    out = subprocess.run([binary] + args, capture_output=True, text=True, timeout=3600)
    if out.returncode != 0:
        raise RuntimeError(f"{binary} {' '.join(args)} failed ({out.returncode}): {out.stderr.strip()}")
    return [json.loads(line) for line in out.stdout.splitlines() if line.startswith("{")]


def steady(rows):
    """Cycles after the first one (the first one also pays for allocating the tables)."""
    return rows[1:] if len(rows) > 1 else rows


def med(rows, key):
    vals = [r[key] for r in rows if r.get(key, -1) is not None and r.get(key, -1) >= 0]
    return statistics.median(vals) if vals else None


def fmt(v, digits=1):
    return "n/a" if v is None else f"{v:,.{digits}f}"


def ratio(a, b):
    return "-" if not a or b is None else f"{b / a:.2f}x"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--poc", required=True)
    ap.add_argument("--ldd", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--scenarios", default="all", help="comma-separated, or 'all'. Known: " + ", ".join(SCENARIOS))
    ap.add_argument("--repeat", type=int, default=3, help="processes per scenario and backend")
    ap.add_argument("--cycles", type=int, default=4, help="add+serialize cycles per process")
    ap.add_argument("--cpu", type=int, default=2, help="core to pin the benchmark to (-1: no pinning)")
    ap.add_argument("--ldd-version", default="?")
    ap.add_argument("--poc-version", default="?")
    ap.add_argument("--quick", action="store_true", help="1 repeat, 2 cycles, smaller scenarios")
    a = ap.parse_args()

    names = list(SCENARIOS) if a.scenarios == "all" else [s.strip() for s in a.scenarios.split(",") if s.strip()]
    unknown = [n for n in names if n not in SCENARIOS and n != "export"]
    if unknown:
        sys.exit(f"unknown scenario(s): {unknown}. Known: {list(SCENARIOS)} and 'export'")
    if a.quick:
        a.repeat, a.cycles = 1, 2
    os.makedirs(a.out, exist_ok=True)
    common = ["--cycles", str(a.cycles), "--cpu", str(a.cpu)]
    # on Windows the process CPU clock is too coarse (~15.6 ms): use the wall clock of the pinned process
    add_key = "add_wall_ns_per_sample" if IS_WINDOWS else "add_ns_per_sample"
    ser_key = "serialize_wall_ms" if IS_WINDOWS else "serialize_cpu_ms"
    clock = "wall time on a pinned core" if IS_WINDOWS else "process CPU time"

    results = {}
    for name in [n for n in names if n != "export"]:
        args = list(SCENARIOS[name][0])
        if a.quick:
            n = int(args[args.index("--samples") + 1])
            args[args.index("--samples") + 1] = str(max(500, n // 10))
        for backend, binary in (("libdatadog", a.ldd), ("poc", a.poc)):
            rows = []
            for r in range(a.repeat):
                print(f"  {name:24s} {backend:10s} run {r + 1}/{a.repeat}", file=sys.stderr, flush=True)
                rows += steady(run(binary, args + common))
            results.setdefault(name, {})[backend] = rows

    export = {}
    if "export" in names or a.scenarios == "all":
        server, url = start_sink()
        for backend, binary in (("libdatadog", a.ldd), ("poc", a.poc)):
            rows = []
            for r in range(a.repeat):
                print(f"  {'export':24s} {backend:10s} run {r + 1}/{a.repeat}", file=sys.stderr, flush=True)
                rows += run(binary, EXPORT_ARGS + ["--url", url, "--cpu", str(a.cpu)])
            export[backend] = rows
        server.shutdown()

    with open(os.path.join(a.out, "results.json"), "w") as f:
        json.dump({"scenarios": results, "export": export}, f, indent=1)

    heap_metric = sorted({r.get("heap_metric", "?") for be in results.values() for rows in be.values() for r in rows}) or ["?"]
    heap_metric = ", ".join(heap_metric)
    lines = [
        "# PoC vs libdatadog benchmark",
        "",
        f"- date: {datetime.datetime.now().isoformat(timespec='seconds')}",
        f"- libdatadog: {a.ldd_version}, PoC: {a.poc_version}",
        f"- machine: {platform.platform()}, {platform.processor() or platform.machine()}, {os.cpu_count()} logical CPUs",
        f"- {a.repeat} process(es) x {a.cycles} cycles per scenario and backend, pinned to CPU {a.cpu}; "
        f"values are medians of the steady-state cycles; CPU = {clock}",
        "- one cycle = add N samples, then serialize (the profile is reused, as a profiler does)",
        f"- heap measured with: {heap_metric}",
        "",
        "## Summary: CPU per cycle (add + serialize)",
        "",
        "| scenario | libdatadog ms | PoC ms | PoC / libdatadog |",
        "|---|---|---|---|",
    ]
    for name, be in results.items():
        n = SAMPLES[name] if not a.quick else max(500, SAMPLES[name] // 10)
        tot = {}
        for backend in ("libdatadog", "poc"):
            vals = [r[add_key] * n / 1e6 + r[ser_key] for r in be[backend]]
            tot[backend] = statistics.median(vals)
        lines.append(f"| {name} | {fmt(tot['libdatadog'])} | {fmt(tot['poc'])} | {ratio(tot['libdatadog'], tot['poc'])} |")

    metrics = [
        (add_key, "add, ns/sample", 0),
        (ser_key, "serialize, ms", 2),
        ("heap_after_add_kb", "heap after adds, KB", 0),
        ("heap_with_encoded_kb", "heap with the encoded profile alive, KB", 0),
        ("serialize_peak_rss_extra_kb", "serialize peak RSS increase, KB (Linux only)", 0),
        ("encoded_bytes", "encoded profile, bytes", 0),
        ("endpoints_cpu_us", "set_endpoint + add_endpoint_count x1000, us", 1),
    ]
    for name, be in results.items():
        lines += ["", f"## {name}: {SCENARIOS[name][1]}", "", "| metric | libdatadog | PoC | PoC / libdatadog |", "|---|---|---|---|"]
        for key, label, digits in metrics:
            l, p = med(be["libdatadog"], key), med(be["poc"], key)
            if key == "endpoints_cpu_us" and not l and not p:
                continue
            lines.append(f"| {label} | {fmt(l, digits)} | {fmt(p, digits)} | {ratio(l, p)} |")

    if export:
        lines += ["", "## Exporter (20 sends of a small profile + a 16 KB attachment to a local sink)", "",
                  "| metric | libdatadog | PoC | PoC / libdatadog |", "|---|---|---|---|"]
        for key, label in (("exporter_new_wall_us", "exporter creation, us (wall)"),
                           ("rss_exporter_new_kb", "RSS increase at exporter creation, KB"),
                           ("send_cpu_us", "send, us CPU" + (" (coarse on Windows)" if IS_WINDOWS else "")),
                           ("send_wall_us", "send, us wall"),
                           ("encoded_bytes", "profile payload, bytes"),
                           ("threads_after", "threads after sends")):
            l, p = med(export["libdatadog"], key), med(export["poc"], key)
            lines.append(f"| {label} | {fmt(l)} | {fmt(p)} | {ratio(l, p)} |")

    report = "\n".join(lines) + "\n"
    with open(os.path.join(a.out, "report.md"), "w") as f:
        f.write(report)
    print(report)


if __name__ == "__main__":
    main()
