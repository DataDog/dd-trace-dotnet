#!/usr/bin/env bash
set -euo pipefail

fixture_repository="${FFE_FIXTURE_REPOSITORY:-DataDog/ffe-system-test-data}"
fixture_ref="${FFE_FIXTURE_REF:-main}"
fixture_destination="${FFE_FIXTURE_DESTINATION:-tracer/test/Datadog.Trace.Tests/FeatureFlags/ffe-system-test-data}"

repo_root="$(git rev-parse --show-toplevel)"
work_dir="$(mktemp -d)"

cleanup() {
  rm -rf "${work_dir}"
}
trap cleanup EXIT

source_dir="${work_dir}/source"
snapshot_dir="${work_dir}/snapshot"

if [[ -z "${fixture_destination}" || "${fixture_destination}" == "." || "${fixture_destination}" == "/" || "${fixture_destination}" == /* ]]; then
  echo "Refusing to update unsafe fixture destination: ${fixture_destination}" >&2
  exit 1
fi

destination_dir="$(
  python3 - "${repo_root}/${fixture_destination}" <<'PY'
import pathlib
import sys

print(pathlib.Path(sys.argv[1]).resolve(strict=False))
PY
)"

if [[ "${destination_dir}" == "${repo_root}" || "${destination_dir}" != "${repo_root}/"* ]]; then
  echo "Refusing to update fixture destination outside the repository: ${destination_dir}" >&2
  exit 1
fi

GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null git init --quiet "${source_dir}"
GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null git -C "${source_dir}" remote add origin "https://github.com/${fixture_repository}.git"
GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null git -C "${source_dir}" fetch --quiet --depth 1 origin "${fixture_ref}"
GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null git -C "${source_dir}" checkout --quiet --detach FETCH_HEAD

source_commit="$(git -C "${source_dir}" rev-parse HEAD)"

if [[ ! -f "${source_dir}/ufc-config.json" || ! -d "${source_dir}/evaluation-cases" ]]; then
  echo "Source ${fixture_repository}@${fixture_ref} does not contain the expected fixture layout" >&2
  exit 1
fi

mkdir -p "${snapshot_dir}"
cp "${source_dir}/ufc-config.json" "${snapshot_dir}/ufc-config.json"
cp -R "${source_dir}/evaluation-cases" "${snapshot_dir}/evaluation-cases"

for file in LICENSE LICENSE-3rdparty.csv NOTICE; do
  if [[ -f "${source_dir}/${file}" ]]; then
    cp "${source_dir}/${file}" "${snapshot_dir}/${file}"
  fi
done

fixture_count="$(
  python3 - "${snapshot_dir}" <<'PY'
import json
import pathlib
import sys

snapshot = pathlib.Path(sys.argv[1])
json.loads((snapshot / "ufc-config.json").read_text())

case_files = sorted((snapshot / "evaluation-cases").glob("*.json"))
if not case_files:
    raise SystemExit("No JSON fixture files found")

case_count = 0
for path in case_files:
    data = json.loads(path.read_text())
    if not isinstance(data, list):
        raise SystemExit(f"{path} must contain a JSON array of test cases")
    case_count += len(data)

if case_count == 0:
    raise SystemExit("No fixture test cases found")

print(case_count)
PY
)"

changed=true
if [[ -d "${destination_dir}" ]] && diff --brief --recursive --exclude=SOURCE.md "${snapshot_dir}" "${destination_dir}" >/dev/null; then
  changed=false
else
  cat > "${snapshot_dir}/SOURCE.md" <<EOF
# FFE Fixture Snapshot

These files are copied from the canonical FFE fixture repository.

Canonical source: https://github.com/${fixture_repository}
Source commit: ${source_commit}

Do not edit these fixtures directly in dd-trace-dotnet. Add or update shared FFE behavior in ffe-system-test-data first, then refresh this snapshot.

The weekly update workflow runs \`.github/scripts/update-ffe-fixtures.sh\` and opens a draft dependency PR only when the fixture contents change.
EOF

  rm -rf "${destination_dir}"
  mkdir -p "$(dirname "${destination_dir}")"
  mv "${snapshot_dir}" "${destination_dir}"
fi

echo "Checked FFE fixtures from ${fixture_repository}@${source_commit}"
echo "Loaded ${fixture_count} JSON fixture cases"
echo "Fixture snapshot changed: ${changed}"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "source_commit=${source_commit}"
    echo "fixture_count=${fixture_count}"
    echo "changed=${changed}"
  } >> "${GITHUB_OUTPUT}"
fi
