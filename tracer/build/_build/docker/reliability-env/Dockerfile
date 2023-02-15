FROM mcr.microsoft.com/dotnet/sdk:6.0 AS dotnet_agent_debug
ARG LINUX_PACKAGE

RUN apt-get update && apt-get install dos2unix

COPY ${LINUX_PACKAGE} /binaries/
RUN ls /binaries/datadog-dotnet-apm-*.tar.gz | cut -d - -f 4 | sed -e 's/\.tar.gz$//' > /binaries/LIBRARY_VERSION
RUN mv /binaries/${LINUX_PACKAGE} /binaries/datadog-dotnet-apm.tar.gz

COPY ddtrace-info.sh query-versions.fsx /binaries/
RUN dos2unix /binaries/ddtrace-info.sh
RUN /binaries/ddtrace-info.sh

RUN touch -c -a -m -d @0 /binaries/*

FROM scratch as collect
COPY --from=dotnet_agent_debug /binaries/datadog-dotnet-apm.tar.gz /datadog-dotnet-apm.tar.gz
COPY --from=dotnet_agent_debug /binaries/LIBRARY_VERSION /LIBRARY_VERSION
COPY --from=dotnet_agent_debug /binaries/LIBDDWAF_VERSION /LIBDDWAF_VERSION
COPY --from=dotnet_agent_debug /binaries/APPSEC_EVENT_RULES_VERSION /APPSEC_EVENT_RULES_VERSION

FROM scratch
COPY --from=collect /* /
