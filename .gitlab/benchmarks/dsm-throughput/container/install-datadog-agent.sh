#!/usr/bin/env bash

export DD_API_KEY="%REPLACE_DD_API_KEY%"
export DD_INSTALL_ONLY=1
export DD_SITE="datadoghq.com"
export DD_HOSTNAME="%HOSTNAME%"
export DD_APM_ENABLED=true
export DD_LOGS_ENABLED=true
export DD_PROCESS_AGENT_ENABLED=true
export DD_APM_ENV=kafka-cluster-test
export DD_ENV=kafka-cluster-test

bash -c "$(curl -L https://dd-agent.s3.amazonaws.com/scripts/install_script_agent7.sh)"
cp /etc/datadog-agent/datadog.yaml /etc/datadog-agent/datadog.yaml.template