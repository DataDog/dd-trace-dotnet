FROM busybox as collect
ARG LINUX_PACKAGE
ARG LIBRARY_VERSION
ARG APPSEC_EVENT_RULES_VERSION
ARG LIBDDWAF_VERSION

RUN mkdir /binaries \
  && echo ${LIBRARY_VERSION} | cat > /binaries/LIBRARY_VERSION \
  && echo ${LIBDDWAF_VERSION} | cat > /binaries/LIBDDWAF_VERSION \
  && echo ${APPSEC_EVENT_RULES_VERSION} | cat > /binaries/APPSEC_EVENT_RULES_VERSION
COPY ${LINUX_PACKAGE} /binaries/datadog-dotnet-apm.tar.gz

FROM scratch
COPY --from=collect /binaries/* /
