ARG TESTER_IMAGE=dd-trace-dotnet/debian-tester:latest

FROM registry.ddbuild.io/ci-identities/ci-identities-ci-job-client:v0.9.0-linux AS ci-identities
FROM ${TESTER_IMAGE}

COPY --from=ci-identities /usr/local/bin/ci-identities-ci-job-client /usr/local/bin/ci-identities-ci-job-client
COPY --from=registry.ddbuild.io/images/datadog-ca-certs:standard /certs/ /usr/local/share/ca-certificates/

RUN chmod -R o-w /usr/local/share/ca-certificates \
    && update-ca-certificates
