#!/usr/bin/env bash

# Three attempts, with a short delay between failures. Callers must bound each
# attempt (for example, with timeout) so an unresponsive service cannot hang CI.
retry() {
    local attempt status
    for attempt in 1 2 3; do
        if "$@"; then
            return 0
        else
            status=$?
        fi
        if [ "$attempt" -lt 3 ]; then
            echo "Attempt $attempt failed (exit $status); retrying in 5 seconds..." >&2
            sleep 5
        fi
    done
    return "$status"
}

# Pass '-' as the first argument to return the response body on stdout, or a
# destination filename to download a file. Remaining arguments go to curl.
curl_with_retry() (
    local destination="$1" response_file http_status
    shift
    response_file=$(mktemp) || exit 1
    trap 'rm -f "$response_file"' EXIT

    # Do not use --fail/--fail-with-body: with --retry-all-errors they would
    # retry permanent HTTP errors such as 404. Check the final status ourselves.
    # --max-time also bounds the last attempt, which can outlast --retry-max-time.
    if http_status=$(curl --silent --show-error --location \
        --retry 3 --retry-all-errors --retry-max-time 180 \
        --connect-timeout 15 --max-time 120 \
        --output "$response_file" --write-out '%{http_code}' "$@"); then
        if [[ "$http_status" != 2?? ]]; then
            echo "Request failed with HTTP $http_status" >&2
            exit 22
        fi
    else
        exit "$?"
    fi

    # A real output file lets curl discard partial responses before retrying.
    # Only expose the body after a successful request; never log error bodies.
    if [ "$destination" = "-" ]; then
        cat "$response_file"
    else
        mv "$response_file" "$destination"
    fi
)
