# GitLab Migration Feasibility — dd-trace-dotnet

**Status:** Investigation
**Date:** 2026-05-05
**Initiative:** [.NET] Investigate steps and blockers for migrating from Azure DevOps (ADO) to GitLab and deprecating ADO while maintaining full build/test coverage (Windows MSI, Linux, macOS).

This document covers two distinct but related migrations:

1. **ADO → GitLab feasibility** (§1–§6 below). The broader question of deprecating the Azure DevOps pipeline and consolidating on GitLab.
2. **GitHub org / GitLab instance split mitigation** (§0 below). Pre-work to keep the existing GitLab pipeline functional through Datadog's separation of internal and OSS GitHub organizations and the corresponding GitLab instance/group reorganization. Scoped to URL/group parameterization.

---

## 0. GitHub org / GitLab instance split — mitigation status

**Why this exists.** Datadog is splitting its GitHub presence into two organizations: internal code moves to `github.com/ddoghq/` (Enterprise Managed Users), open-source code stays at `github.com/DataDog/`. GitLab is following: OSS builds will run on a separate runner fleet (and possibly a separate GitLab instance) from internal builds. Any CI file that hardcodes the `DataDog/` GitLab group or GitHub org will need to be edited at cutover. Parameterizing now means migration day is a variable flip, not a code change.

### 0.1 Parameterization scheme

Three variables are defined at the top of `.gitlab-ci.yml`:

| Variable | Today's value | Purpose |
|---|---|---|
| `GITLAB_GROUP` | `DataDog` | GitLab group path for **internal** cross-repo references (triggers, scripts, clones) |
| `GITLAB_GROUP_OSS` | `DataDog` | GitLab group path for **OSS** cross-repo references (currently `dd-trace-dotnet-aws-lambda-layer`) |
| `GITHUB_ORG_INTERNAL` | `DataDog` | GitHub org for internal repos — used in `git insteadOf` redirects and `dd-octo-sts` scopes |

These variables are defined in the top-level `variables:` block, which means they propagate to child pipelines triggered via `trigger: include:` (child pipelines inherit global parent variables by default) and to all `script:` / job `variables:` contexts at runtime. They do **not** work inside `include:` keywords — see §0.3.

OSS GitHub org references (`DataDog/dd-trace-dotnet`, `DataDog/system-tests`, and similar) are left hardcoded because the OSS org stays at `github.com/DataDog/`.

### 0.2 What has been parameterized

| File | Parameterized refs | Status |
|---|---|---|
| `.gitlab-ci.yml` | `aws-lambda-layer.trigger.project`, `benchmark-serverless` script + curl args, `benchmark-serverless-trigger.trigger.project` | Done in PR #8426 |
| `.gitlab/benchmarks/dsm-throughput.yml` | Internal git clone, `insteadOf` redirect (both `DataDog/` and `${GITHUB_ORG_INTERNAL}/` covered) | Done in PR #8426 |
| `.gitlab/benchmarks/macrobenchmarks.yml` | All four `git clone …/benchmarking-platform` calls; `DDOCTOSTS_SCOPE` for `benchmarking-platform` | Clones done in PR #8426; `DDOCTOSTS_SCOPE` done in follow-up |
| `.gitlab/benchmarks/microbenchmarks.yml` | `git clone …/benchmarking-platform` | Done in PR #8426 |

### 0.3 What stays hardcoded — and why

**Three `include: project:` references stay hardcoded** because GitLab does not expand top-level `variables:` inside `include:` keywords (only project-level, group-level, instance-level, and predefined CI/CD variables are evaluated at include-resolution time, per [GitLab docs](https://docs.gitlab.com/ee/ci/yaml/includes.html)). Defining the variables at GitLab project-level would lift the limitation but introduces a hidden coupling between repo state and GitLab UI state that would silently break forks and fresh project mirrors.

These three lines are the **manual migration touchpoints** at cutover — each is flagged with a `MIGRATION TOUCHPOINT` comment in source:

1. `.gitlab-ci.yml:14` — `include: project: 'DataDog/apm-reliability/apm-sdks-benchmarks'`
2. `.gitlab/benchmarks/macrobenchmarks.yml:2` — `include: project: 'DataDog/benchmarking-platform-tools'`
3. `.gitlab/benchmarks/macrobenchmarks.yml:4` — `include: project: 'DataDog/benchmarking-platform-tools'`

**OSS GitHub references stay hardcoded** because `github.com/DataDog/dd-trace-dotnet` and the broader OSS GitHub org are not moving:

- `.gitlab/download-single-step-artifacts.sh` — downloads from this repo's OSS GitHub releases.
- `.gitlab/benchmarks/microbenchmarks/bp-runner.windows.yml` — `repo: DataDog/dd-trace-dotnet` (×4) in bp-runner experiment definitions.
- `.gitlab/benchmarks/microbenchmarks/infrastructure/instance.yml` — clones `dd-trace-dotnet.git` for bp-runner config.
- `.gitlab/benchmarks/microbenchmarks/infrastructure/ami.yml`, `.gitlab/benchmarks/macrobenchmarks.yml:766` — comment URLs.
- `.gitlab/benchmarks/microbenchmarks.yml:95` — `DDOCTOSTS_SCOPE: "DataDog/dd-trace-dotnet"` (OSS scope).
- `.azure-pipelines/ultimate-pipeline.yml` and `.github/workflows/*` — all `github.com/DataDog/...` references in the ADO pipeline and GitHub Actions are either to this OSS repo (`dd-trace-dotnet`) or to `system-tests`, both OSS.

### 0.4 Out-of-scope mitigations (need infra/coordination, not code edits)

The following will need attention at or before cutover but are not solvable with YAML edits in this repo:

- **Runner tags** — `tags: ["windows-v2:2022"]` and `tags: ["arch:amd64"]` may need to change if the OSS runner fleet uses a different tag namespace.
- **Docker registry paths** — `registry.ddbuild.io/ci/dd-trace-dotnet/...` and `registry.ddbuild.io/images/ci/images` may move under an OSS-instance registry.
- **`id_tokens` audience** — `aud: ci-identities` may need a different value on the OSS GitLab instance.
- **GitLab schedules, secrets/CI variables, branch protections, webhooks** — none of these live in the repo; all must be recreated on the target project at cutover.
- **External callers** — `dd-trace-dotnet-aws-lambda-layer` and any other downstream pipelines that trigger us or are triggered by us need bilateral coordination on project paths.
- **`CI_IDENTITIES_CLIENT_URL`** S3 path and `s3://dd-windowsfilter/builds/tracer/` publish destination — confirm these are reachable from OSS runners.

### 0.5 Migration-day playbook (just for org-split mitigation)

1. Set the new values for `GITLAB_GROUP`, `GITLAB_GROUP_OSS`, and `GITHUB_ORG_INTERNAL` in `.gitlab-ci.yml`'s top-level `variables:` block. Single commit. All trigger jobs, scripts, and child-pipeline job variables update automatically.
2. Update the three `MIGRATION TOUCHPOINT`-flagged `include: project:` lines by hand in the same commit. Grep `MIGRATION TOUCHPOINT` to find them.
3. Validate on a feature branch before merging: `trigger:` jobs and `include:` resolution can both fail silently with a literal `${VAR}` in the path.
4. Coordinate with infra on items in §0.4 before flipping the variables.

---

## TL;DR

- We already run a **hybrid CI**: ADO does the heavy lifting (build + tests across Windows/Linux/macOS/ARM64); GitLab does Windows signed-build, OCI packaging, OSS serverless artifacts, benchmarks, and system-tests gating.
- The ADO pipeline is **5,464 lines, 75 user-authored jobs across 77 stages** (plus ~153 status-update jobs injected at runtime by `update-github-status-jobs.yml` — 3 per stage × 51 invocations). Approximate platform split: ~17 Windows / ~18 Linux x64 / ~10 ARM64 / ~8 macOS / ~5 meta; the remaining ~17 jobs are tool/coverage/benchmark/system-test/installer-smoke jobs that span multiple platforms. GitLab currently has **17 jobs** in `.gitlab-ci.yml` plus jobs pulled in via `one-pipeline.locked.yml`.
- The main blockers are: (1) **macOS runners** in Datadog GitLab (no like-for-like analogue to GitHub-hosted `macos-14` today); (2) **ARM64 runner capacity**; (3) **GitHub PR trigger + commit-status flow** for OSS contributors; (4) mechanical translation of ~5,464 lines of YAML and ~20 step templates.

---

## 1. Current state

### 1.1 Azure DevOps (`.azure-pipelines/ultimate-pipeline.yml`)

ADO is the primary CI. The pipeline runs on every PR, master push, release/hotfix push, and on two daily schedules (3am UTC build, 4am UTC debug run). It owns:

| Area | Jobs (approx.) | Key pools / images |
|---|---|---|
| Windows build / unit / integration / MSI / FleetInstaller / smoke | ~17 | `azure-managed-windows-x64-2` |
| Linux x64 build / unit / integration / profiler (containerized debian/centos7/alpine) | ~18 | `azure-managed-linux-x64-1` + `run-in-docker.yml` |
| Linux ARM64 build + tracer R2R + profiler + universal + tests | ~10 | `azure-managed-linux-arm64-1` |
| macOS native tracer / native loader / artifact upload / debug build / unit tests / sample build / cppcheck / smoke tool tests | ~8 | `vmImage: macos-14` (Microsoft-hosted) |
| Meta (variables, merge_commit_id capture, GitHub token, exploration tests) | ~5 | `azure-managed-linux-tasks` |

ADO-specific constructs in active use (file:line refs use `.azure-pipelines/ultimate-pipeline.yml`):

- 20 YAML step templates in `.azure-pipelines/steps/` (`run-in-docker.yml`, `update-github-status.yml`, `install-dotnet.yml`, `install-msi.yml`, `clean-docker-containers.yml`, etc.) plus 1 helper shell script (`ensure-docker-ready-linux.sh`)
- `${{ if }}` compile-time conditionals (e.g. `run-in-docker.yml:37`)
- Cross-stage variable outputs (e.g. `stageDependencies.merge_commit_id.fetch.outputs['set_sha.sha']`, line 226)
- `$[in(…)]`, `$[eq(…)]`, `$[coalesce(…)]` runtime expression expansion (lines 88–143)
- ADO predefined variables: `Build.SourceBranch`, `Build.Reason`, `Build.BuildId`, `Build.BuildNumber`, `System.PullRequest.*`, `System.DefaultWorkingDirectory`, `System.JobDisplayName`, `System.TeamProjectId`
- Container resources for sidecar Datadog agent (`dd_agent`, `dd_agent_no_pull`, lines 159–179)
- Marketplace tasks: `PublishTestResults@2`, `PublishCodeCoverageResults@1`, `PublishPipelineArtifact@1`, `DownloadPipelineArtifact@2`, `UsePythonVersion@0`, `DotNetCoreCLI@2`, `UseDotNet@2`, `ExtractFiles@1`, `reportgenerator@4`
- GitHub PR trigger (`pr:` block, line 21) and bidirectional GitHub status updates (`update-github-status*.yml` templates)
- Schedule triggers (`schedules:`, lines 42–63)
- DD logger telemetry variables (`DD_LOGGER_*`, lines 121–147) feeding CI Visibility back into Datadog

### 1.2 GitLab (`.gitlab-ci.yml` + `.gitlab/`)

GitLab today owns the **release-critical, signing, and downstream-trigger** parts:

- `build` (Windows, runs in `dd-trace-dotnet-docker-build:ci-identities` container): `BuildTracerHome BuildProfilerHome BuildNativeLoader BuildDdDotnet PublishFleetInstaller PackageTracerHome ZipSymbols SignDlls SignMsi DownloadWinSsiTelemetryForwarder` — i.e. **the signed Windows artifacts that ship to customers**.
- `publish` (Windows): uploads signed artifacts to S3 (`s3://dd-windowsfilter/builds/tracer/`).
- `download-single-step-artifacts`, `download-serverless-artifacts`: pull artifacts that **ADO produced** for OCI packaging and serverless (`.gitlab-ci.yml:139` and `:182` comments: "Artifacts come from Azure pipeline"; the actual download logic lives in `.gitlab/download-single-step-artifacts.sh` and `.gitlab/download-serverless-artifacts.sh`).
- `package-oci`, `aws-lambda-layer` triggers, `configure_system_tests`, system-tests gates — all via the included `one-pipeline.locked.yml`.
- Benchmarks: `macrobenchmarks`, `microbenchmarks`, `dsm_throughput`, `benchmark-serverless`.
- The shared `validate_supported_configurations_v2_local_file` and `update_central_configurations_version_range_v2` config-management jobs.

**Critical fact:** Code signing for Windows binaries and MSIs **already lives in GitLab**, not ADO. Signing uses an internal `c:/devtools/windows-code-signer.exe` invoked from `tracer/build/_build/Build.Gitlab.cs:51` (`SignDlls`) and `:74` (`SignMsi`), gated by `SIGN_WINDOWS=true`. ADO MSIs are intentionally unsigned (see comment block at `ultimate-pipeline.yml:4229`). Signing infrastructure is therefore **not** a migration blocker — it is a migration *anchor*: any migration plan can lean on what already exists.

### 1.3 GitHub Actions (`.github/workflows/`)

35 workflows. Most are administrative/automation (release drafts, code-freeze automation, vendored package bumps, AAS deploys, hotfix branch creation), but **14 trigger on `pull_request:`** and 9 of those are PR validation gates that act as required checks: the `verify_*.yml` family (`verify_solution_changes_are_persisted`, `verify_files_without_nullability`, `verify_generated_pipeline_is_updated`, `verify_integrations_map_added`, `verify_source_generated_changes_are_persisted`, `verify_app_trimming_changes_are_persisted`, `verify_span_metadata_markdown_is_updated`), plus `auto_check_snapshots.yml` and `codeql-analysis.yml`.

These run on `windows-latest`/`ubuntu-latest` GitHub-hosted runners and execute lightweight Nuke targets (e.g. `RegenerateSolutions`, snapshot diffs). They are **in scope** for the migration: a complete cutover to GitLab needs to either (a) keep them on GitHub Actions and treat them as a permanent non-GitLab dependency, or (b) port them to GitLab jobs. Treat as small mechanical work, but not zero.

---

## 2. What a full migration must replicate

To deprecate ADO while maintaining coverage, GitLab must take over (job counts below are *test-only*, excluding the build/package jobs already covered separately in §1.1's platform totals):

1. **Windows test matrix**: unit tests, integration tests, MSI integration tests, FleetInstaller tests, profiler tests, smoke tests, debug-config builds. (~14 test jobs; §1.1's "~17 Windows" total includes ~3 build/package jobs on top of these.)
2. **Linux x64 test matrix**: unit, integration, profiler (with ASAN/UBSAN/TSAN variants), debug, exploration, smoke. Must run inside the Debian/CentOS7/Alpine images in `tracer/build/_build/docker/`. (~16 test jobs; §1.1's "~18 Linux x64" total includes ~2 build/package jobs.)
3. **Linux ARM64 build + test matrix**: tracer, tracer R2R, profiler, universal, plus unit/integration tests. (~10 jobs total — split is roughly ~4 build/package + ~6 test.)
4. **macOS build + test**: 2 native build jobs (`native_tracer`, `native_loader_and_managed`) plus `upload_artifacts` on `macos-14`, the debug-config `macos` build, `unit_tests_macos`, `build_samples_macos`, the macOS cppcheck job under stage `static_analysis_checks_tracer`, and `smoke_macos_tool_tests`. (~8 jobs.)
5. **Cross-stage SHA pinning**: the `merge_commit_id` stage that records master’s SHA at pipeline start so all later stages run against a stable merge commit. (Replaceable with GitLab `dotenv` artifacts.)
6. **GitHub PR triggers** and **GitHub commit-status updates** for `dd-trace-dotnet` (this is an OSS repo at `github.com/DataDog/dd-trace-dotnet` and most contributor PRs are gated by ADO status checks today).
7. **Schedule triggers** for daily and daily-debug runs.
8. **Test result publishing** — currently `PublishTestResults@2` plus the DD CI Visibility logger; GitLab supports JUnit artifact ingestion natively, but the DD logger pipeline expects ADO predefined variables and would need rewiring (see `DD_LOGGER_*` variables, `ultimate-pipeline.yml:121–147`).
9. **Sidecar Datadog Agent** for trace-emission tests (`resources: containers:` → GitLab `services:`). Mechanical translation, but `services:` semantics differ on Windows runners.
10. **The 20 ADO step templates** under `.azure-pipelines/steps/`. These are reusable units — most translate to GitLab YAML anchors / `extends:` / `!reference:`, but a few (`update-github-status*.yml`, `install-msi.yml`, `run-in-docker.yml`) encode non-trivial logic.

---

## 3. Blockers (in order of severity)

### 3.1 CI wall time

- The dominant known component on the existing GitLab Windows build is **container image pull time** for `dd-trace-dotnet-docker-build:ci-identities` (the build image is large; without runner-side caching every job re-pulls it). This is not a runner-CPU or architectural limitation, and is mitigable via runner-side image caching.
- Everything else (Linux x64, ARM64, fan-out) is **unmeasured** — we don't yet have wall-time data for those workloads on GitLab.

This significantly reduces the economic objection to migration. It does not eliminate it — Linux/ARM64 fan-out timing still needs to be measured during the pilot — but the headline blocker on the one Windows workload that already runs in GitLab has a known fix in hand.

Other plausible time costs to watch for during the pilot, in order of suspected impact:

- **Artifact transfer**: GitLab artifact upload/download uses object storage with no in-pipeline cache layer comparable to ADO `PipelineArtifact`. The matrix has ~10 large artifacts (tracer-home, profiler-home, MSI bundles) consumed by ~30 downstream jobs. Worth measuring before assuming parity.
- **Job startup overhead**: ADO uses stage-level `dependsOn` to fan jobs out narrow → wide → narrow. GitLab DAG via `needs:` works the same way, but per-job image-pull / runner-allocation tax compounds across 30+ fan-out jobs. Mitigation: same runner-side image caching that solves the Windows build case.
- **Linux/ARM64 runner CPU ceiling**: shared GitLab runners have smaller CPU/memory ceilings than the dedicated `azure-managed-linux-x64-1` / `linuxArm64Pool`. May or may not matter — measure first, escalate if it does.

### 3.2 macOS runner availability

ADO uses `vmImage: macos-14` (Microsoft-hosted, M1) for ~8 macOS jobs (2 native builds, 1 artifact upload, 1 debug build, unit tests, sample build, cppcheck under `static_analysis_checks_tracer`, smoke tool tests). Datadog's GitLab does not maintain a comparable first-class macOS runner pool. Workarounds and their costs:

- **Self-hosted Mac fleet**: requires sourcing hardware, setup, OS lifecycle management, and Apple-specific signing/notarization plumbing. This is months of infra work.
- **Trigger a downstream GitHub Actions workflow** from GitLab for macOS jobs only: feasible (we already trigger `dd-trace-dotnet-aws-lambda-layer` cross-project), but means we never *fully* deprecate non-GitLab CI; we just move the dependency from ADO to GitHub Actions.
- **Drop macOS coverage**: not acceptable per the initiative's "maintain full build/test coverage (incl. … macOS)" requirement.

This is the **most concrete technical blocker** and must be addressed by infra before a migration is even technically possible.

### 3.3 ARM64 capacity at parity

ADO uses `azure-managed-linux-arm64-1` for ~10 jobs. GitLab Datadog has ARM64 runner tags (e.g. `arch:arm64`), but capacity at the same fan-out and per-job CPU profile needs confirmation from infra. Today these jobs run in containerized self-hosted ADO pools; replicating them on GitLab shared runners may be slower than 1:1.

### 3.4 GitHub PR integration

`dd-trace-dotnet` is OSS. PRs come from contributors who do not have GitLab access. ADO's GitHub PR trigger + commit-status flow is well-trodden. GitLab supports this via external pipeline triggers and GitHub apps, but:

- We currently push pipeline status back to GitHub via custom `update-github-status*.yml` templates that hit the GitHub API with a bot token. This logic needs rewriting against GitLab job lifecycle hooks.
- Required-status-check rules on GitHub branch protection currently reference ADO check names. Every renamed/replaced check is a coordination cost.
- GitLab does not see GitHub fork branches as native; we'd need a polling/mirror mechanism (already used for some Datadog OSS repos, but adds latency).

### 3.5 ADO YAML construct translation

Mechanical but voluminous. Notable patterns that don't translate one-to-one:

| ADO construct | GitLab equivalent | Friction |
|---|---|---|
| `template:` includes with typed `parameters:` | `include:` + YAML anchors / `!reference` / `extends:` | Loses type safety and compile-time evaluation |
| `${{ if eq(parameters.X, true) }}` | `rules: - if: $X == "true"` | Compile-time → runtime; doesn't structurally remove the job |
| Stage outputs (`stageDependencies.X.Y.outputs[…]`) | `dotenv` artifact + `needs: artifacts: true` | More plumbing, less ergonomic |
| `Build.SourceBranch`, `Build.BuildNumber`, `System.PullRequest.*` | `CI_COMMIT_REF_NAME`, `CI_PIPELINE_ID`, `CI_MERGE_REQUEST_*` | `DD_LOGGER_*` env vars (declared at `ultimate-pipeline.yml:121-147` and read by `tracer/build/_build/MetricHelper.cs`) need a GitLab-side mapping shim |
| `condition: and(succeeded(), eq(...))` | `rules:` with `if:` | Different mental model; common bugs around `when: on_success` defaults |
| `resources: containers:` sidecars | `services:` | Differs on Windows runners; Datadog Agent sidecar pattern needs validation |
| `PublishTestResults@2`, `PublishCodeCoverageResults@1` | `artifacts: reports: junit/coverage_report` | Native, but DD CI Visibility logger pipeline tied to ADO env vars |

### 3.6 Secrets, identities, and S3 publish path

GitLab uses `id_tokens:` for OIDC + AWS role assumption (`ci-identities-gitlab-job-client.exe assume-role`). This works today for the Windows signed-build job. A migration must extend the same pattern to all jobs that currently rely on ADO service connections — DD API key consumption, NuGet feed pushes, GitHub bot tokens for status updates.

---

## 4. Steps (what a real migration would look like)

Phased to validate at each step rather than commit-then-pray.

### Phase 0 — Baseline & infra prerequisites (out of dev-team's control)

- Confirm GitLab Linux/Windows/ARM64 runner capacity at parity with current ADO pools (sustained throughput, not single-run benchmarks).
- Resolve macOS strategy (provision Mac runners, or accept GitHub Actions cross-trigger as the long-term solution and document that we are not actually deprecating non-GitLab CI).
- Confirm GitHub PR trigger + commit-status path works for OSS-fork PRs.

### Phase 1 — Move one platform end-to-end as a pilot

Recommended platform: **Linux x64**, because the runner story is most mature on GitLab and the docker-in-docker pattern already works in `.gitlab-ci.yml`'s build job.

- Translate `build_linux_tracer`, `build_linux_profiler`, `unit_tests_linux`, one integration-test slice. Run **in parallel with ADO** for ≥2 weeks. Compare wall time, test pass rate, flake rate.
- Establish DD CI Visibility logger variable mapping (a small Nuke patch in `MetricHelper.cs` — which is where `DD_LOGGER_*` env vars are actually consumed — to read GitLab `CI_*` env vars when `GITLAB_CI=true`).

### Phase 2 — Linux ARM64

Reuse Phase 1 patterns. Validate ARM64 runner capacity under load.

### Phase 3 — Windows test matrix

Adapt the existing GitLab Windows build job (already 2h timeout, signed-build) to spawn the test sub-jobs. The hardest part is `msi_integration_tests_windows` and `fleet_installer_tests` which install/uninstall MSIs and are stateful.

### Phase 4 — macOS

Conditional on Phase 0 macOS resolution. If GitHub Actions cross-trigger is the answer, this becomes a different shape of work (GHA workflow + GitLab `trigger:` job), not a migration.

### Phase 5 — Cutover & deprecate ADO

- Move GitHub branch-protection required checks to GitLab check names.
- Move schedule triggers.
- Decommission `.azure-pipelines/`. Keep `noop-pipeline.yml` for one release cycle as a safety net.
- Update `AGENTS.md` and `docs/development/CI/TroubleshootingCIFailures.md` to remove ADO references and document the GitLab pipeline structure.

---

## 5. Effort estimate

Realistic full-parity effort (assuming infra is ready):

| Phase | Effort | Notes |
|---|---|---|
| Phase 0 (infra) | Not dev-team effort, but **gates everything** | Months of infra-team work, likely |
| Phase 1 (Linux x64 pilot, parallel) | 2–3 dev-weeks | Including DD logger shim and parallel-run measurement |
| Phase 2 (ARM64) | 1–2 dev-weeks | Lower if Phase 1 patterns generalize |
| Phase 3 (Windows tests) | 2–3 dev-weeks | MSI/FleetInstaller tests are stateful; signing already works |
| Phase 4 (macOS) | 1–4 dev-weeks | Wide range; depends on infra resolution |
| Phase 5 (cutover) | 1 dev-week | Plus 2–3 weeks of post-cutover stabilization |
| **Total** | **7–13 dev-weeks** | Assuming Phase 0 is solved externally |

---

## 6. Recommendation

Proceed incrementally, with clear-eyed scoping of the remaining real blockers. Specifically:

1. **Validate the runner-side image-caching mitigation on the existing GitLab Windows build first.** It is the cheapest, highest-information experiment because the Windows build already runs on GitLab today (signed-build job in `.gitlab-ci.yml`). If runner-side caching of `dd-trace-dotnet-docker-build:ci-identities` lands the GitLab Windows build at ≤1.2× the equivalent ADO Windows build wall time, the headline economic argument against migration evaporates and the rest of the work is mechanical-but-tractable. If it doesn't, we have hard data to escalate before sinking weeks into broader porting. Note: ADO Windows builds are unsigned and serve testing purposes; GitLab builds are signed for shipping — adjust for that delta when comparing.
2. **Run a Linux x64 pilot in parallel with ADO** (2–3 dev-weeks). This is the same ask as before — we still need to measure rather than assume Linux-side timing. But the framing shifts from "decide whether to migrate at all" to "characterize the remaining cost surface".
3. **Unblock macOS and ARM64 runner capacity with infra now.** These remain real blockers (see [§3.2](#32-macos-runner-availability), [§3.3](#33-arm64-capacity-at-parity)) regardless of how the Windows-build pilot goes. macOS is the largest concrete unknown — filing the runner-capacity request now keeps it off the critical path.
4. **Keep the Build.Gitlab.cs / Nuke abstraction as the migration vehicle.** Same Nuke targets already run on either CI; don't let new ADO-only logic creep into `Build.Steps.cs` if it can live in a target invoked from either pipeline. This reduces per-job translation risk.

