FROM mcr.microsoft.com/dotnet/sdk:6.0 AS dotnet_agent_debug
RUN apt-get update && apt-get install dos2unix

COPY ./tooling/ci/ddtrace-info.sh ./tooling/ci/query-versions.fsx ./tooling/ci/binaries* /binaries/

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
