#!/bin/sh

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
fixture="$script_dir/targeting-regex-conformance.json"
validator="$script_dir/validate-targeting-regex-conformance.jq"
temporary_file=$(mktemp)

cleanup() {
  rm -f "$temporary_file"
}

handle_signal() {
  signal=$1
  trap - "$signal"
  cleanup
  kill -s "$signal" "$$"
}

trap cleanup 0
trap 'handle_signal HUP' HUP
trap 'handle_signal INT' INT
trap 'handle_signal TERM' TERM

expect_invalid() {
  description=$1
  mutation=$2

  jq "$mutation" "$fixture" >"$temporary_file"
  if jq -e -f "$validator" "$temporary_file" >/dev/null; then
    echo "validator accepted invalid fixture: $description" >&2
    exit 1
  fi
}

jq -e -f "$validator" "$fixture" >/dev/null

expect_invalid \
  "compile failure with a true match result" \
  '.cases |= map(if .id == "rejected-byte-escape" then .expectedMatch = true else . end)'

expect_invalid \
  "empty semantics" \
  '.semantics = {}'

expect_invalid \
  "empty portable syntax contract" \
  '.portableSyntax.accepted = [] | .portableSyntax.rejected = []'

echo "targeting regex conformance validator tests passed"
