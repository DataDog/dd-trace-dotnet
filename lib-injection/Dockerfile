FROM scratch AS dotnet_build
ARG LINUX_PACKAGE

# The ADD command does more than COPY, so it can directly copy a local directory or it can copy over a `tar.gz` file and automatically extract its contents into the destination
ADD ${LINUX_PACKAGE} /

FROM alpine:3.18.3

ARG UID=10000
RUN addgroup -g 10000 -S datadog && \
    adduser -u ${UID} -S datadog -G datadog
USER ${UID}
WORKDIR /datadog-init

COPY copy-lib.sh /datadog-init/copy-lib.sh
# using a separate stage for holding the assets - can allow us to build this dockerfile remotely,
# and replace the assets when needed
COPY --from=dotnet_build . /datadog-init/monitoring-home
