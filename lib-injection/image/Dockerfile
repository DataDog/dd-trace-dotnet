# This image provides the files needed to install the dotnet library
# and auto instrument dotnet applications in containerized environments.
FROM busybox

ARG UID=10000
RUN addgroup -g 10000 -S datadog && \
    adduser -u ${UID} -S datadog -G datadog \

USER ${UID}

WORKDIR /datadog-init

ADD copy-lib.sh /datadog-init/copy-lib.sh