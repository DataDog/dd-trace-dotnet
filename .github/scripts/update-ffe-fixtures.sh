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
git clone --filter=blob:none --no-checkout "https://github.com/${fixture_repository}.git" "${source_dir}"
git -C "${source_dir}" fetch --depth 1 origin "${fixture_ref}"
git -C "${source_dir}" checkout --detach FETCH_HEAD

source_commit="$(git -C "${source_dir}" rev-parse HEAD)"
destination_dir="${repo_root}/${fixture_destination}"

if [[ -z "${fixture_destination}" || "${fixture_destination}" == "." || "${fixture_destination}" == "/" ]]; then
  echo "Refusing to update unsafe fixture destination: ${fixture_destination}" >&2
  exit 1
fi

case "${destination_dir}" in
  "${repo_root}"/*) ;;
  *)
    echo "Refusing to update fixture destination outside the repository: ${destination_dir}" >&2
    exit 1
    ;;
esac

if [[ ! -f "${source_dir}/ufc-config.json" || ! -d "${source_dir}/evaluation-cases" ]]; then
  echo "Source ${fixture_repository}@${fixture_ref} does not contain the expected fixture layout" >&2
  exit 1
fi

rm -rf "${destination_dir}"
mkdir -p "${destination_dir}"

cp "${source_dir}/ufc-config.json" "${destination_dir}/ufc-config.json"
cp -R "${source_dir}/evaluation-cases" "${destination_dir}/evaluation-cases"

for file in LICENSE LICENSE-3rdparty.csv NOTICE; do
  if [[ -f "${source_dir}/${file}" ]]; then
    cp "${source_dir}/${file}" "${destination_dir}/${file}"
  fi
done

cat > "${destination_dir}/SOURCE.md" <<EOF
# FFE Fixture Snapshot

These files are copied from the canonical FFE fixture repository.

Canonical source: https://github.com/${fixture_repository}
Source commit: ${source_commit}

Do not edit these fixtures directly in dd-trace-dotnet. Add or update shared FFE behavior in ffe-system-test-data first, then refresh this snapshot.
EOF

fixture_count="$(
  python3 - "${destination_dir}" <<'PY'
import json
import pathlib
import sys

destination = pathlib.Path(sys.argv[1])
config_path = destination / "ufc-config.json"
cases_dir = destination / "evaluation-cases"

json.loads(config_path.read_text())

case_files = sorted(cases_dir.glob("*.json"))
if not case_files:
    raise SystemExit(f"No JSON fixture files found in {cases_dir}")

case_count = 0
for path in case_files:
    data = json.loads(path.read_text())
    if not isinstance(data, list):
        raise SystemExit(f"{path} must contain a JSON array of test cases")
    case_count += len(data)

if case_count == 0:
    raise SystemExit(f"No test cases found in {cases_dir}")

print(case_count)
PY
)"

echo "Updated FFE fixtures from ${fixture_repository}@${source_commit}"
echo "Loaded ${fixture_count} JSON fixture cases"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "source_commit=${source_commit}"
    echo "fixture_count=${fixture_count}"
  } >> "${GITHUB_OUTPUT}"
fi
