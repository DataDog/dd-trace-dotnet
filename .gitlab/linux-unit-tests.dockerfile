# The caller must supply the job-specific tester image built with the selected SDK.
ARG TESTER_IMAGE

FROM ${TESTER_IMAGE}

# Pin the multi-architecture index so x64 and ARM64 jobs use reviewed trust roots.
COPY --from=registry.ddbuild.io/images/datadog-ca-certs:standard@sha256:451a0209af8cff55760f798e99fa5a5a220b3ea94aef9d0da9208f533be90cea /certs/ /usr/local/share/ca-certificates/

RUN chmod -R o-w /usr/local/share/ca-certificates \
    && update-ca-certificates
