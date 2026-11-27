$ErrorActionPreference = 'Stop'

$env:DD_HOSTNAME = "$(hostname)-$($env:EXTENDED_SCENARIO_NAME -replace '_','-')"
if (-not $env:DD_API_KEY) {
    throw "DD_API_KEY is not set"
    exit 1
}

# Future Improvements: This is a workaround to facilitate debugging with local
# runs. The CI integration glue should ideally be handled by 'build-and-run-*'
# scripts, managed by bp-runner itself.
if (-not $env:GITLAB_CI) {
    $env:CI_COMMIT_REF_NAME = "local"
    $env:CI_JOB_NAME = "local"
    $env:CI_JOB_ID = [int][double]::Parse((Get-Date -UFormat %s))
    $env:CI_PIPELINE_ID = $env:CI_JOB_ID

    # See https://gist.github.com/gregjhogan/2350eb60d02aa759c9d269c3fc6265b1
    $env:CI_COMMIT_SHA = -join ((48..57) + (97..122) | Get-Random -Count 20 | ForEach-Object {[char]$_})
    $env:CI_COMMIT_SHORT_SHA = $env:CI_COMMIT_SHA.Substring(0, 7)
}

@"
api_key: $env:DD_API_KEY
hostname: $env:DD_HOSTNAME
site: datadoghq.com
dogstatsd_non_local_traffic: true

tags:
  - bp_app:dotnet-throughput
  - bp_branch:$env:CI_COMMIT_REF_NAME
  - bp_commit_sha:$env:CI_COMMIT_SHORT_SHA
  - bp_project:dd-trace-dotnet
  - ci_job_id:$env:CI_JOB_ID
  - ci_job_name:$env:CI_JOB_NAME
  - ci_pipeline_id:$env:CI_PIPELINE_ID
  - scenario:$env:EXTENDED_SCENARIO_NAME
  - endpoint:$env:ENDPOINT

process_config:
  process_collection:
    enabled: true

  process_discovery:
    enabled: true
    interval: 10s
"@ | Set-Content -Path "C:\ProgramData\Datadog\datadog.yaml"

if ($env:DD_REMOTE_CONFIGURATION_ENABLED -as [bool]) {
    @"

remote_configuration:
  enabled: true
"@ | Add-Content -Path "C:\ProgramData\Datadog\datadog.yaml"
}

@"
init_config:
  pid_cache_duration: 15
  access_denied_cache_duration: 15
  shared_process_list_cache_duration: 15

instances:
  - name: dotnet-throughput
    search_string:
      - AspNetCoreSimpleController
    exact_match: false
    tags:
      - bp_app:dotnet-throughput
      - bp_branch:$env:CI_COMMIT_REF_NAME
      - bp_commit_sha:$env:CI_COMMIT_SHORT_SHA
      - bp_project:dd-trace-dotnet
      - ci_job_id:$env:CI_JOB_ID
      - ci_job_name:$env:CI_JOB_NAME
      - ci_pipeline_id:$env:CI_PIPELINE_ID
      - scenario:$env:EXTENDED_SCENARIO_NAME
      - endpoint:$env:ENDPOINT

  - name: k6
    search_string:
      - k6
    exact_match: false
    tags:
      - bp_app:dotnet-throughput
      - bp_branch:$env:CI_COMMIT_REF_NAME
      - bp_commit_sha:$env:CI_COMMIT_SHORT_SHA
      - bp_project:dd-trace-dotnet
      - ci_job_id:$env:CI_JOB_ID
      - ci_job_name:$env:CI_JOB_NAME
      - ci_pipeline_id:$env:CI_PIPELINE_ID
      - scenario:$env:EXTENDED_SCENARIO_NAME
      - endpoint:$env:ENDPOINT
"@ | Set-Content -Path "C:\ProgramData\Datadog\conf.d\process.d\conf.yaml"
