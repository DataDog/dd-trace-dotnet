# Used only during manual publication, on top of the selected test environment.
ARG TESTER_IMAGE

FROM ${TESTER_IMAGE}

# Pin the multi-architecture index so x64 and ARM64 jobs use reviewed trust roots.
COPY --from=registry.ddbuild.io/images/datadog-ca-certs:standard@sha256:451a0209af8cff55760f798e99fa5a5a220b3ea94aef9d0da9208f533be90cea /certs/ /usr/local/share/ca-certificates/

RUN chmod -R o-w /usr/local/share/ca-certificates \
    && update-ca-certificates

# Parse the STS response in test-job setup, before starting NUKE.
RUN if command -v apk >/dev/null 2>&1; then \
        apk add --no-cache jq; \
    else \
        apt-get update && apt-get install -y --no-install-recommends jq \
        && rm -rf /var/lib/apt/lists/*; \
    fi
