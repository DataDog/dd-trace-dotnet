FROM mcr.microsoft.com/dotnet/sdk:6.0 AS dotnet_agent_debug
ARG LINUX_PACKAGE

RUN apt-get update && apt-get install dos2unix

# The ADD command does more than COPY, so it can directly copy a local directory or it can copy over a `tar.gz` file and automatically extract its contents into the destination
ADD ${LINUX_PACKAGE} /binaries/

COPY ddtrace-info.sh query-versions.fsx /binaries/
RUN mv /binaries/${LINUX_PACKAGE} /binaries/datadog-dotnet-apm.tar.gz

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
