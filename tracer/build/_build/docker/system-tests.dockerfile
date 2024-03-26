FROM busybox as collect
ARG LINUX_AMD64_PACKAGE
ARG LINUX_ARM64_PACKAGE
ARG LIBRARY_VERSION
ARG APPSEC_EVENT_RULES_VERSION
ARG LIBDDWAF_VERSION
ARG TARGETARCH

RUN mkdir /binaries \
  && echo ${LIBRARY_VERSION} | cat > /binaries/LIBRARY_VERSION \
  && echo ${LIBDDWAF_VERSION} | cat > /binaries/LIBDDWAF_VERSION \
  && echo ${APPSEC_EVENT_RULES_VERSION} | cat > /binaries/APPSEC_EVENT_RULES_VERSION
COPY ${LINUX_AMD64_PACKAGE} /binaries/amd64/datadog-dotnet-apm.tar.gz
COPY ${LINUX_ARM64_PACKAGE} /binaries/arm64/datadog-dotnet-apm.tar.gz
RUN echo "TARGETARCH=${TARGETARCH}"
RUN if [[ "${TARGETARCH}" == "amd64" ]] ; then mv /binaries/amd64/datadog-dotnet-apm.tar.gz /binaries/datadog-dotnet-apm.tar.gz ; else mv /binaries/arm64/datadog-dotnet-apm.tar.gz /binaries/datadog-dotnet-apm.tar.gz ; fi

FROM scratch
COPY --from=collect /binaries/* /
