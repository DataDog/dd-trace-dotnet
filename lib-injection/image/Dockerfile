# This image provides the files needed to install the dotnet library
# and auto instrument dotnet applications in containerized environments.
FROM busybox

ARG UID=10000
RUN addgroup -g 10000 -S datadog
RUN adduser -S -G datadog -u ${UID} datadog
USER ${UID}

WORKDIR /datadog-init
ADD datadog-dotnet-apm-2.19.0.tar.gz ./datadog-dotnet-apm-2.19.0.tar.gz
ADD copy-lib.sh ./copy-lib.sh
