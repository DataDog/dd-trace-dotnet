# FFE Fixture Snapshot

These rules-based fixtures (`ufc-config.json` and `evaluation-cases/`) are copied from the canonical FFE fixture repository.

Canonical source: https://github.com/DataDog/ffe-system-test-data
Source commit: f6c8ad870531d041eaef42286899c93ad4d6e0e9

Do not edit these fixtures directly in dd-trace-dotnet. Add or update shared FFE behavior in ffe-system-test-data first, then refresh this snapshot.

The weekly update workflow runs `./tracer/build.sh UpdateFfeFixtures` and opens a draft test PR only when the allowed fixture contents change.
