#!/usr/bin/env bash

set -e

# Important to have this beast of DD_HOSTNAME, otherwise Datadog API doesn't change tags correctly on dd-agent reruns
export DD_HOSTNAME="$(hostname)-${EXTENDED_SCENARIO_NAME//_/-}"
if [ -z "$DD_API_KEY" ]; then
  export DD_API_KEY=$(aws ssm get-parameter --region us-east-1 --name ci.$CI_PROJECT_NAME.dd_api_key --with-decryption --query "Parameter.Value" --out text)
fi

cat <<EOT >/etc/datadog-agent/datadog.yaml
api_key: $DD_API_KEY
hostname: $DD_HOSTNAME
site: datadoghq.com

tags:
  - bp_app:dotnet-throughput
  - bp_branch:$CI_COMMIT_REF_NAME
  - bp_commit_sha:$CI_COMMIT_SHORT_SHA
  - bp_project:dd-trace-dotnet
  - ci_job_id:$CI_JOB_ID
  - ci_job_name:$CI_JOB_NAME
  - ci_pipeline_id:$CI_PIPELINE_ID
  - scenario:$EXTENDED_SCENARIO_NAME
  - endpoint:$ENDPOINT

otlp_config:
  receiver:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

process_config:
  process_collection:
    enabled: true

  process_discovery:
    enabled: true
    interval: 10s
EOT

if [[ "$DD_REMOTE_CONFIGURATION_ENABLED" == true || "$DD_REMOTE_CONFIGURATION_ENABLED" == 1 ]]; then
  cat <<EOT >>/etc/datadog-agent/datadog.yaml
remote_configuration:
  enabled: true
EOT
fi

cat <<EOT > /etc/datadog-agent/conf.d/process.d/conf.yaml
init_config:
  pid_cache_duration: 15
  access_denied_cache_duration: 15
  shared_process_list_cache_duration: 15

instances:
  - name: dotnet-throughput
    search_string:
      - dotnet
    exact_match: true

  - name: k6
    search_string:
      - k6
EOT
