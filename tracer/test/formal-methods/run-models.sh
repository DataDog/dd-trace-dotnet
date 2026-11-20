#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly TLA_JAR="${1:-${TLA_TOOLS_JAR:-}}"

if [[ -z "${TLA_JAR}" || ! -f "${TLA_JAR}" ]]; then
  echo "usage: $0 /path/to/tla2tools.jar" >&2
  exit 2
fi

run_known_counterexample() {
  local module="$1"
  local config="$2"
  local output
  local status

  set +e
  output="$(java -XX:+UseParallelGC -cp "${TLA_JAR}" tlc2.TLC \
    -workers 1 -config "${SCRIPT_DIR}/${config}" "${SCRIPT_DIR}/${module}" 2>&1)"
  status=$?
  set -e

  if [[ ${status} -eq 0 ]]; then
    echo "ERROR: ${config} unexpectedly satisfied its contract" >&2
    echo "${output}" >&2
    return 1
  fi

  if [[ "${output}" != *"Error: Invariant"* && "${output}" != *"Error: Temporal properties were violated"* ]]; then
    echo "ERROR: ${config} failed for the wrong reason" >&2
    echo "${output}" >&2
    return 1
  fi

  echo "known counterexample confirmed: ${config}"
}

run_known_counterexample RcmDelivery.tla RcmApplyFailure.cfg
run_known_counterexample RcmDelivery.tla RcmRemoveFailure.cfg
run_known_counterexample RcmDelivery.tla RcmUnrelatedRemoval.cfg
run_known_counterexample RcmDelivery.tla RcmLateSubscriber.cfg
run_known_counterexample BatchingSink.tla BatchSaturation.cfg
run_known_counterexample BatchingSink.tla BatchFailed.cfg
run_known_counterexample BatchingSink.tla BatchFalseAck.cfg
run_known_counterexample BatchingSink.tla BatchAfterDispose.cfg
run_known_counterexample DataStreams.tla DsmFailedSend.cfg
run_known_counterexample DataStreams.tla DsmFalseAck.cfg
run_known_counterexample DataStreams.tla DsmClose.cfg
run_known_counterexample AgentWriterClose.tla AgentWriterConcurrentClose.cfg
run_known_counterexample TelemetryRetry.tla TelemetryFinalFailure.cfg
run_known_counterexample TelemetryRetry.tla TelemetryLogFailure.cfg
run_known_counterexample RcmCapabilities.tla RcmConcurrentCapabilities.cfg
run_known_counterexample TraceTransportRetry.tla TraceTransportException.cfg
