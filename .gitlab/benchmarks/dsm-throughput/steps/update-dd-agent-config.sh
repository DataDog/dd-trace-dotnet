#!/usr/bin/env bash

set -e

export DD_HOSTNAME=$(hostname)
if [ -z "$DD_API_KEY" ]; then
  export DD_API_KEY=$(aws ssm get-parameter --region us-east-1 --name ci.$CI_PROJECT_NAME.dd_api_key --with-decryption --query "Parameter.Value" --out text)
fi

cat <<EOT >/etc/datadog-agent/datadog.yaml
api_key: $DD_API_KEY
hostname: $DD_HOSTNAME
site: datadoghq.com
dogstatsd_non_local_traffic: true

tags:
  - bp_app:dotnet-dsm-server
  - bp_branch:$CI_COMMIT_REF_NAME
  - bp_commit_sha:$CI_COMMIT_SHORT_SHA
  - bp_project:dotnet-dsm-throughput
  - ci_job_id:$CI_JOB_ID
  - ci_job_name:$CI_JOB_NAME
  - ci_pipeline_id:$CI_PIPELINE_ID

process_config:
  process_collection:
    # Enable if you need to collect process metrics for the processes configured below
    enabled: false

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

# Uncomment if you need to collect process metrics
# cat <<EOT >/etc/datadog-agent/conf.d/process.d/conf.yaml
# init_config:
#   pid_cache_duration: 15
#   access_denied_cache_duration: 15
#   shared_process_list_cache_duration: 15

# instances:
#   - name: your-sut
#     search_string:
#       - seach-string-to-match-your-sut
#     exact_match: false

#     tags:
#       - bp_app:your-sut
#       - bp_branch:$CI_COMMIT_REF_NAME
#       - bp_commit_sha:$CI_COMMIT_SHORT_SHA
#       - bp_project:your-project
#       - ci_job_id:$CI_JOB_ID
#       - ci_job_name:$CI_JOB_NAME
#       - ci_pipeline_id:$CI_PIPELINE_ID

#   - name: k6
#     search_string:
#       - k6
#     exact_match: false
#     tags:
#       - bp_app:your-sut
#       - bp_branch:$CI_COMMIT_REF_NAME
#       - bp_commit_sha:$CI_COMMIT_SHORT_SHA
#       - bp_project:your-project
#       - ci_job_id:$CI_JOB_ID
#       - ci_job_name:$CI_JOB_NAME
#       - ci_pipeline_id:$CI_PIPELINE_ID
# EOT
