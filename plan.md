# Plan: Modernize dd-trace-dotnet Microbenchmarks with bp-runner

## Goal

Replace the current shell-script-based benchmark execution with bp-runner, starting with a minimal "dry run" benchmark to validate the infrastructure.

## Current State

- **CI Job**: `run-benchmarks` in `.gitlab/benchmarks/microbenchmarks.yml`
- **Execution**: Clones `benchmarking-platform` repo, runs `./platform/steps/run-windows-benchmarks.sh`
- **Windows Script**: `run-benchmarks.ps1` in benchmarking-platform repo does:
  1. Prepare env vars (fetch DD_API_KEY from SSM)
  2. Clone dd-trace-dotnet
  3. Build tracer
  4. Run benchmarks via `tracer\build.cmd RunBenchmarks`
  5. Download baseline metadata from S3
  6. Upload results to S3
- **AMI**: Built from `ami.yaml` in benchmarking-platform, has .NET SDK, VS Build Tools, bp-runner pre-installed
- **Instance**: Simple `instance.yaml` that clones benchmarking-platform and runs `run-benchmarks.ps1`

## Target State

- **bp-runner.windows.yml** in dd-trace-dotnet repo controls benchmark execution
- **ami.yml** in dd-trace-dotnet repo builds AMI with bp-runner from dev branch
- **instance.yml** provision file clones dd-trace-dotnet, builds, and runs bp-runner
- **bp-infra launch** triggers the provision from GitLab CI with `--env-regex`
- Minimal first step: run a single benchmark with `--job dry`

## Files to Create/Modify

### 1. `.gitlab/benchmarks/bp-runner.windows.yml` (NEW)

bp-runner config that runs a single fast benchmark:

```yaml
experiments:
  - name: dd-trace-dotnet-microbenchmarks
    steps:
      - name: run-microbenchmarks
        run: run_microbenchmarks
        repo: DataDog/dd-trace-dotnet
        # We only run benchmarks for the candidate, and compare results
        # with previously stored results for master
        baseline_branch: ""
        how_to_run_benchmarks: |
          cd $env:CODE_SRC\tracer\test\benchmarks\Benchmarks.Trace
          dotnet run -c Release -f net8.0 --no-build -- `
            --job dry `
            --filter "*SpanBenchmark.StartFinishSpan" `
            -r net8.0 `
            --exporters json
```

### 2. `benchmarking-platform/dd-trace-dotnet-micro/ephemeral-infra/ami-test.yaml` ✅ DONE

**Note**: Using benchmarking-platform repo because it has dd-octo-sts access via
`gitlab.benchmarking-platform.read-contents` policy.

Created `ami-test.yaml` with:

- `BP_RUNNER_BRANCH: "augusto/run-microbenchmarks-parallelize-windows"`
- Test SSM parameter: `/windows-benchmarking/dd-trace-dotnet-microbenchmarks-ami-test-id`
- `install_bp_runner` step using the standard `install.ps1` pattern (clones repo to temp, copies bp-runner to `C:\app\bp-runner`, runs install.ps1)
- bp-runner check added to `test_setup`

Also created CI job `build-dd-trace-dotnet-microbenchmarks-ami-test` in `.gitlab-ci.yml`:

- Runs automatically (not manual) and is interruptible
- Uses dd-octo-sts with scope `DataDog` and policy `gitlab.benchmarking-platform.read-contents`
- Passes GITHUB_TOKEN to instance via `--env .env`

### 3. `.gitlab/benchmarks/infrastructure/instance.yml` (NEW)

Provision file for bp-infra that sets up the Windows instance and runs bp-runner:

```yaml
name: &provision_name "dd-trace-dotnet-microbenchmarks-test"

init_environment:
  DD_API_KEY_SSM_PARAMETER: "/windows-benchmarking/dd-api-key"
  # S3 bucket for benchmark results
  BP_INFRA_ARTIFACTS_BUCKET_NAME: "windows-benchmarking-results-us-east-1"

source_ami:
  # Use test AMI (built from benchmarking-platform repo with bp-runner from dev branch)
  ami_id_ssm_parameter: "/windows-benchmarking/dd-trace-dotnet-microbenchmarks-ami-test-id"

tags:
  Name: *provision_name

provision_steps:
  - check_cpu_model
  - clone_dd_trace_dotnet
  - build_tracer
  # TODO (Phase 2): Download baseline results from S3
  # - download_baseline_results
  - run_benchmarks
  # TODO (Phase 2): Upload results to S3
  # - upload_results

check_cpu_model:
  remote_command: |
    $ErrorActionPreference = "Stop"
    $cpuModel = (Get-WmiObject -Class Win32_Processor).Name
    if ($cpuModel -notlike "*8259CL CPU*") {
        Write-Error "CPU model is not Intel(R) Xeon(R) Platinum 8259CL"
    }
    Write-Output "CPU model is Intel(R) Xeon(R) Platinum 8259CL"

clone_dd_trace_dotnet:
  populate_env: true
  remote_command: |
    $ErrorActionPreference = "Stop"
    $branch = if ($env:CI_COMMIT_REF_NAME) { $env:CI_COMMIT_REF_NAME } else { "master" }
    Write-Output "Cloning dd-trace-dotnet branch: $branch"
    # Use GITHUB_TOKEN for authenticated clone (handles private repos and rate limits)
    if ($env:GITHUB_TOKEN) {
        git clone -b $branch --single-branch "https://x-access-token:$env:GITHUB_TOKEN@github.com/DataDog/dd-trace-dotnet.git" C:\app\candidate
    } else {
        git clone -b $branch --single-branch https://github.com/DataDog/dd-trace-dotnet.git C:\app\candidate
    }

build_tracer:
  remote_command: |
    $ErrorActionPreference = "Stop"
    cd C:\app\candidate
    tracer\build.cmd CreateRequiredDirectories Restore CompileManagedSrc DownloadLibDdwaf CopyLibDdwaf

# TODO (Phase 2): Download baseline results from S3 for comparison
# download_baseline_results:
#   populate_env: true
#   remote_command: |
#     $ErrorActionPreference = "Stop"
#     $latestResultsPrefix = "$env:CI_PROJECT_NAME/_latest"
#     $baselineDir = "C:\app\baseline_results"
#     New-Item -ItemType Directory -Path $baselineDir -Force | Out-Null
#     try {
#         Read-S3Object `
#             -BucketName $env:BP_INFRA_ARTIFACTS_BUCKET_NAME `
#             -KeyPrefix $latestResultsPrefix `
#             -Folder $baselineDir
#         Write-Output "Downloaded baseline results from S3"
#     } catch {
#         Write-Output "No baseline results found in S3. Comparison will not be possible."
#     }

run_benchmarks:
  populate_env: true
  remote_command: |
    $ErrorActionPreference = "Stop"

    # Fetch DD_API_KEY from SSM if not set
    if (-not $env:DD_API_KEY) {
        if ($env:DD_API_KEY_SSM_PARAMETER) {
            $env:DD_API_KEY = (Get-SSMParameter -Name $env:DD_API_KEY_SSM_PARAMETER -WithDecryption $true).Value
        }
    }

    cd C:\app\candidate
    bp-runner .gitlab\benchmarks\bp-runner.windows.yml --debug

# TODO (Phase 2): Upload results to S3
# upload_results:
#   populate_env: true
#   remote_command: |
#     $ErrorActionPreference = "Stop"
#     $resultsDir = "C:\app\candidate\tracer\artifacts\build_data\benchmarks"
#     $s3Prefix = "$env:CI_PROJECT_NAME/$env:CI_COMMIT_REF_NAME/$env:CI_JOB_ID/reports"
#
#     Write-Output "Uploading results to s3://$env:BP_INFRA_ARTIFACTS_BUCKET_NAME/$s3Prefix"
#     Write-S3Object `
#         -BucketName $env:BP_INFRA_ARTIFACTS_BUCKET_NAME `
#         -KeyPrefix $s3Prefix `
#         -Folder $resultsDir `
#         -CannedACLName "bucket-owner-full-control" `
#         -Recurse
#
#     # Update latest results if on master
#     if ($env:CI_COMMIT_REF_NAME -eq "master") {
#         $latestPrefix = "$env:CI_PROJECT_NAME/_latest"
#         Write-Output "Updating latest master results at s3://$env:BP_INFRA_ARTIFACTS_BUCKET_NAME/$latestPrefix"
#         Write-S3Object `
#             -BucketName $env:BP_INFRA_ARTIFACTS_BUCKET_NAME `
#             -KeyPrefix $latestPrefix `
#             -Folder $resultsDir `
#             -CannedACLName "bucket-owner-full-control" `
#             -Recurse
#     }
```

### 4. `.gitlab/benchmarks/microbenchmarks.yml` (MODIFY)

Update `run-benchmarks` job to use bp-infra with `--env-regex`:

```yaml
run-benchmarks:
  stage: benchmarks
  tags: ["arch:amd64"]
  timeout: 2h
  image: registry.ddbuild.io/images/benchmarking-platform-tools-ubuntu:dd-trace-dotnet-micro
  id_tokens:
    DDOCTOSTS_ID_TOKEN:
      aud: dd-octo-sts
  rules:
    - if: $CI_COMMIT_REF_NAME =~ /^v[0-9]+\.[0-9]+\.[0-9]+(-prerelease)?$/
      when: never
    - if: $CI_COMMIT_REF_NAME == "master"
      interruptible: false
    - interruptible: true
  artifacts:
    name: "artifacts"
    when: always
    paths:
      - reports/
    expire_in: 3 months
  variables:
    # Scope for dd-octo-sts to get GitHub token for cloning dd-trace-dotnet
    DDOCTOSTS_SCOPE: "DataDog/dd-trace-dotnet"
    DDOCTOSTS_POLICY: "gitlab.github-access.read-contents"
    AWS_REGION: "us-east-1"
    BP_INFRA_ARTIFACTS_BUCKET_NAME: "windows-benchmarking-results-us-east-1"
    CLEANUP: "true"
    ARTIFACTS_DIR: "reports"
  before_script:
    # Fetch GitHub token using dd-octo-sts (writes to /tmp/github-token)
    - !reference [.dd-octo-sts-setup, before_script]
  script:
    - mkdir -p reports
    # Export GITHUB_TOKEN for bp-infra to pass to the instance
    - export GITHUB_TOKEN=$(cat /tmp/github-token)
    - CLEANUP_ARG=$([[ "$CLEANUP" == "false" ]] && echo "--no-cleanup" || echo "")
    - |
      bp-infra launch \
        --provision .gitlab/benchmarks/infrastructure/instance.yml \
        --region "${AWS_REGION}" \
        --os windows \
        --env-regex "^(CI_|DD_|BP_|PR_|ARTIFACTS_|GITHUB_)" \
        --bypass-stack-destroy \
        $CLEANUP_ARG
  after_script:
    - |
      if [ "$CLEANUP" == "true" ]; then
        bp-infra cleanup \
          --provision .gitlab/benchmarks/infrastructure/instance.yml \
          --region "${AWS_REGION}" \
          --os windows \
          --bypass-stack-destroy || true
      fi
    # TODO: Add result fetching, analysis, PR comment steps
```

## Implementation Steps

### Phase 0: Prerequisites ✅ DONE

1. ~~Open PR on dd-trace-dotnet to grant dd-octo-sts access to benchmarking-platform-tools~~
   - Using benchmarking-platform repo instead (has existing dd-octo-sts access via `gitlab.benchmarking-platform.read-contents`)
2. ✅ Created `ami-test.yaml` in benchmarking-platform repo with:
   - `BP_RUNNER_BRANCH: "augusto/run-microbenchmarks-parallelize-windows"`
   - `install_bp_runner` step using `install.ps1` pattern
   - Test SSM parameter: `/windows-benchmarking/dd-trace-dotnet-microbenchmarks-ami-test-id`
3. ✅ Created CI job `build-dd-trace-dotnet-microbenchmarks-ami-test` in benchmarking-platform
   - Uses dd-octo-sts with scope `DataDog` and policy `gitlab.benchmarking-platform.read-contents`
   - Passes GITHUB_TOKEN to instance via `--env .env`
4. ✅ Built test AMI successfully - available at `/windows-benchmarking/dd-trace-dotnet-microbenchmarks-ami-test-id`

### Phase 1: Minimal Validation (IN PROGRESS)

1. ✅ Test AMI built with bp-runner from dev branch
2. Create `.gitlab/benchmarks/infrastructure/instance.yml` in dd-trace-dotnet
3. Create `.gitlab/benchmarks/bp-runner.windows.yml` with single dry-run benchmark
4. Update `.gitlab/benchmarks/microbenchmarks.yml` to use bp-infra with `--env-regex`
5. Test that it runs and produces output

### Phase 1.5: Move AMI to dd-trace-dotnet (FUTURE)

1. After bp-runner changes are merged to main, update AMI to use `main` branch
2. Optionally move AMI definition to dd-trace-dotnet if dd-octo-sts access is granted
3. Update production AMI SSM parameter

### Phase 2: S3 Upload & Results

1. Add S3 upload to bp-runner.windows.yml or instance.yml
2. Add result fetching in after_script
3. Verify results appear in artifacts

### Phase 3: Full Benchmark Run

1. Expand `how_to_run_benchmarks` to run all benchmarks with `--job dry`
2. Test full benchmark suite runs

### Phase 4: Parallelization

1. Add `parallelize` option to bp-runner config
2. Split benchmarks by category/runtime
3. Aggregate results

### Phase 5: Analysis & PR Comments

1. Create `bp-runner.analyze.yml`
2. Create `bp-runner.pr-comment.yml`
3. Wire up post-benchmark analysis

## Key Differences from Current Setup

| Aspect | Current | New |
|--------|---------|-----|
| Benchmark script location | benchmarking-platform repo | dd-trace-dotnet repo |
| Configuration | Shell scripts | YAML (bp-runner) |
| Env var passing | Manual `.env` file | `--env-regex` |
| bp-runner branch | Production | Dev branch (for testing) |
| AMI SSM parameter | Production | Test parameter |

## Dependencies

- bp-runner with `run_microbenchmarks` template from `augusto/run-microbenchmarks-parallelize-windows` branch
- bp-infra for Windows instance provisioning
- Test AMI built with dev bp-runner
