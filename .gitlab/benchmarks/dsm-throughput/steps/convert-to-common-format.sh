#!/usr/bin/env bash
#
# Convert per-experiment dotnet-timeit JSON artifacts to the bp-analyzer common
# format under candidate-<experiment>.converted.json so upload_to_bp_api can
# pick them up.

set -ex

cd "$ARTIFACTS_DIR"

CI_JOB_DATE=$(date +%s)
COMMIT_SHA="${CI_COMMIT_SHA:-}"
COMMIT_REF="${CI_COMMIT_REF_NAME:-}"
PIPELINE_ID="${CI_PIPELINE_ID:-}"
JOB_ID="${CI_JOB_ID:-}"

# BP backend requires several non-framework-specific parameters on every
# benchmark upload (benchmark_backend/api/benchmarks/benchmarks_restapi.py).
# Compute them here and pass via --extra-params below. Fixture comparison with
# fayssal/migrate-micros-dotnet shows "Unknown" is an acceptable string for
# host fields that aren't reliably introspectable.

# git_commit_date as Unix timestamp. GitLab provides CI_COMMIT_TIMESTAMP as
# ISO-8601; convert to epoch seconds. Fall back to the job date.
if [ -n "${CI_COMMIT_TIMESTAMP:-}" ]; then
  COMMIT_DATE=$(date -d "$CI_COMMIT_TIMESTAMP" +%s 2>/dev/null || echo "$CI_JOB_DATE")
else
  COMMIT_DATE="$CI_JOB_DATE"
fi

# cpu_model: Linux /proc/cpuinfo, falling back to Unknown. Strip any double
# quotes so the value can be safely interpolated into the JSON literal below.
CPU_MODEL=$(grep -m1 'model name' /proc/cpuinfo 2>/dev/null | sed 's/.*: //' | tr -d '"')
[ -z "$CPU_MODEL" ] && CPU_MODEL="Unknown"

# kernel_version: uname -r.
KERNEL_VERSION=$(uname -r 2>/dev/null | tr -d '"')
[ -z "$KERNEL_VERSION" ] && KERNEL_VERSION="Unknown"

# Convert every per-experiment JSON to candidate-<name>.converted.json.
# Skip already-converted/uploaded artifacts so reruns are idempotent.
for f in *.json; do
  [ -e "$f" ] || continue
  case "$f" in
    *.converted.json|*.bp-api-uploaded.json) continue ;;
  esac

  base="${f%.json}"
  outpath="$ARTIFACTS_DIR/candidate-${base}.converted.json"

  # The DSM-throughput benchmark uploads two experiments per run; the BP UI
  # diffs them as baseline vs candidate. `tracer-no-dsm` is the control
  # (tracer on, DSM off) and `dsm-enabled` is the variant under test — same
  # axis `steps/compare-results.py` uses when computing overhead.
  #
  # Override the scenario name per-experiment so the two uploads show up as
  # distinct, self-documenting benchmarks in the BP UI (the timeit-derived
  # default of `Default Samples.KafkaBenchmark` would otherwise collide for
  # both experiments and obscure what each one is measuring). bp-analyzer
  # merges `extra-params` after the converter's parameters, so the
  # `scenario` key here wins.
  case "$base" in
    tracer-no-dsm)
      BASELINE_OR_CANDIDATE="baseline"
      SCENARIO="DSM Throughput - DSM off (baseline)"
      ;;
    dsm-enabled)
      BASELINE_OR_CANDIDATE="candidate"
      SCENARIO="DSM Throughput - DSM on"
      ;;
    *)
      BASELINE_OR_CANDIDATE="candidate"
      SCENARIO="DSM Throughput - $base"
      ;;
  esac

  echo "Converting $f -> $(basename "$outpath") [$BASELINE_OR_CANDIDATE / $SCENARIO]"
  bp-analyzer convert \
    --framework=TimeItSharp \
    --extra-params="{\"experiment\":\"$base\",\"scenario\":\"$SCENARIO\",\"ci_job_date\":\"$CI_JOB_DATE\",\"ci_job_id\":\"$JOB_ID\",\"ci_pipeline_id\":\"$PIPELINE_ID\",\"git_commit_sha\":\"$COMMIT_SHA\",\"git_commit_date\":\"$COMMIT_DATE\",\"git_branch\":\"$COMMIT_REF\",\"cpu_model\":\"$CPU_MODEL\",\"kernel_version\":\"$KERNEL_VERSION\",\"baseline_or_candidate\":\"$BASELINE_OR_CANDIDATE\"}" \
    --outpath="$outpath" \
    "$f"
done

echo "Conversion complete:"
ls -la "$ARTIFACTS_DIR"/candidate-*.converted.json
