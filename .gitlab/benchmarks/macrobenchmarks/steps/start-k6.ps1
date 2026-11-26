$ErrorActionPreference = "Stop"

Write-Output ""
Write-Output "------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------"
Write-Output ""
Write-Output ""
Write-Output "You can monitor macrobenchmark progress in https://ddstaging.datadoghq.com/dashboard/rgu-p7u-5fq?tpl_var_bp_branch%5B0%5D=$env:CI_COMMIT_REF_NAME&tpl_var_bp_commit_sha%5B0%5D=$env:CI_COMMIT_SHORT_SHA&tpl_var_ci_pipeline_id%5B0%5D=$env:CI_PIPELINE_ID"
Write-Output ""
Write-Output ""
Write-Output "------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------"
Write-Output ""

$env:K6_STATSD_ENABLE_TAGS = "true"

$logsDir = "C:\BenchmarkArtifacts"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
$k6OutputFile = "$logsDir\$env:EXTENDED_SCENARIO_NAME.json.gz"

k6 run `
    --no-usage-report `
    --out statsd `
    --out "json=$k6OutputFile" `
    "$env:REPOSITORY_ROOT/k6/script.js"

# Add this back when benchmark_analyzer is implemented on Windows
    # --out "json=/dev/stdout" `
    # | benchmark_analyzer convert --framework=k6streaming `
    # --extra-params="{""cpu_model"":""$env:CPU_MODEL"", ""ci_job_date"":""$env:CI_JOB_DATE"", ""ci_job_id"":""$env:CI_JOB_ID"", ""ci_pipeline_id"":""$env:CI_PIPELINE_ID"", ""git_commit_sha"":""$env:CI_COMMIT_SHA"", ""git_commit_date"":""$env:COMMIT_DATE"", ""git_branch"":""$env:CI_COMMIT_REF_NAME""}" `
    # --metrics="[""agg_http_req_duration_min"", ""agg_http_req_duration_p50"", ""agg_http_req_duration_p75"", ""agg_http_req_duration_p95"", ""agg_http_req_duration_p99"", ""agg_http_req_duration_max"", ""throughput"", ""dropped_iterations"", ""data_sent"", ""data_received""]" `
    # --outpath="$env:ARTIFACTS_DIR/candidate-${env:EXTENDED_SCENARIO_NAME}.converted.json" `
    # /dev/stdin