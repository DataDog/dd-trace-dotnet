ARG TESTER_IMAGE

FROM registry.ddbuild.io/ci-identities/ci-identities-gitlab-job-client:v0.6.3-linux-amd64 AS ci-identities
FROM ${TESTER_IMAGE}

COPY --from=ci-identities /ci-identities-gitlab-job-client /usr/local/bin/ci-identities-gitlab-job-client
COPY --from=registry.ddbuild.io/images/datadog-ca-certs:standard /certs/ /usr/local/share/ca-certificates/

RUN chmod -R o-w /usr/local/share/ca-certificates \
    && update-ca-certificates
