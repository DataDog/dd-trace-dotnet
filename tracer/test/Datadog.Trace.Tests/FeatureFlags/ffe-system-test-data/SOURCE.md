# FFE Fixture Snapshot

These files are copied from the canonical FFE fixture repository.

Canonical source: https://github.com/DataDog/ffe-system-test-data
Source commit: a4966e2ee78f15b0b36f204417ceac9895079262

Do not edit these fixtures directly in dd-trace-dotnet. Add or update shared FFE behavior in ffe-system-test-data first, then refresh this snapshot.

The weekly update workflow runs `./tracer/build.sh UpdateFfeFixtures` and opens a draft test PR only when the allowed fixture contents change.