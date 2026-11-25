#!/bin/sh
set +x
set -eu

trap 'unset DD_STS_OIDC_TOKEN' EXIT

if [ -z "$(printf '%s' "${DD_LOGGER_DD_API_KEY:-}" | tr -d '[:space:]')" ]; then
    if [ -z "$(printf '%s' "${DD_STS_OIDC_TOKEN:-}" | tr -d '[:space:]')" ]; then
        echo 'CI Visibility credentials are required: DD_STS_OIDC_TOKEN is unavailable' >&2
        exit 1
    fi

    oidc_token=$DD_STS_OIDC_TOKEN
    unset DD_STS_OIDC_TOKEN
    # Send the header on stdin so the token is not exposed in curl's arguments.
    if ! response=$(printf 'header = "Authorization: Bearer %s"\n' "$oidc_token" | \
        curl --config - --fail --silent --show-error --connect-timeout 15 --max-time 60 \
        'https://dd-sts.us1.ddbuild.io/sts/datadog/exchange?policy=apm-sdks-api-key'); then
        echo 'CI Visibility credential exchange failed' >&2
        exit 1
    fi

    if ! DD_LOGGER_DD_API_KEY=$(printf '%s' "$response" | \
        jq -er '.api_key | select(type == "string") | select(test("\\S"))' 2>/dev/null); then
        echo 'CI Visibility credential exchange returned no valid API key' >&2
        exit 1
    fi
    export DD_LOGGER_DD_API_KEY
    unset oidc_token response
    echo 'CI Visibility API key configured using dd-sts'
fi

unset DD_STS_OIDC_TOKEN
# Keep failure, skip, retry and hang diagnostics without routine per-test progress.
export DD_TEST_LOG_ROUTINE_EVENTS=false
exec sh .gitlab/run-nuke-linux-container.sh \
    RunManagedUnitTests \
    --framework "$FRAMEWORK" \
    --RunManagedUnitTestsFromArtifacts true \
    --NugetPackageDirectory /project/packages
