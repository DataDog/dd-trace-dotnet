#!/usr/bin/env bash

set -e

if [ "$BP_INFRA_TEST" = "true" ]; then
    echo "BP_INFRA_TEST set to '$BP_INFRA_TEST'. Skipping upload to BP UI."
    exit 0
fi

ARTIFACTS_DIR="${ARTIFACTS_DIR:-./artifacts}"

shopt -s nullglob
CONVERTED_RESULTS=("$ARTIFACTS_DIR"/candidate*.converted.json)
shopt -u nullglob

if [ ${#CONVERTED_RESULTS[@]} -eq 0 ]; then
  echo "ERROR: No converted results found in $ARTIFACTS_DIR"
  echo "Make sure to run fetch-results.sh first."
  exit 1
fi

for CONVERTED_JSON in "$ARTIFACTS_DIR"/candidate*.converted.json; do
  [ -f "$CONVERTED_JSON" ] || continue
  
  echo "Uploading $CONVERTED_JSON to Benchmarking Platform service..."
  STATUS_CODE=$(curl --retry 3 --retry-max-time 60 \
    -s -o /dev/null -w "%{http_code}" \
    --form file=@"$CONVERTED_JSON" \
    "https://benchmarking-service.us1.prod.dog/benchmarks/upload/$CI_PROJECT_NAME")

  if test "$STATUS_CODE" -ne 200; then
    echo "ERROR: Uploading to BP UI failed with status code '$STATUS_CODE'"
    exit 1
  fi

  echo "SUCCESS! Uploaded $CONVERTED_JSON to Benchmarking Platform service!"
done

echo "All uploads complete!"

