FROM scratch AS dotnet_build
COPY ./glibc /glibc/

FROM busybox

ARG UID=10000
RUN addgroup -g 10000 -S datadog && \
    adduser -u ${UID} -S datadog -G datadog
USER ${UID}
WORKDIR /datadog-init

# using a separate stage for holding the assets - can allow us to build this dockerfile remotely,
# and replace the assets when needed
COPY --from=dotnet_build ./glibc /datadog-init/
COPY copy-lib.sh /datadog-init/copy-lib.sh
