ARG TESTER_IMAGE=dd-trace-dotnet/debian-tester:latest

FROM ${TESTER_IMAGE}

COPY --from=registry.ddbuild.io/images/datadog-ca-certs:standard /certs/ /usr/local/share/ca-certificates/

RUN chmod -R o-w /usr/local/share/ca-certificates \
    && update-ca-certificates
