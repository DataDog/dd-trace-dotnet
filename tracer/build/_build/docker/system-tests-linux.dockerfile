FROM busybox as collect
ARG PACKAGE_VERSION


RUN mkdir /binaries \
  && echo ${PACKAGE_VERSION} | cat > /binaries/PACKAGE_VERSION 

COPY datadog-dotnet-apm_${PACKAGE_VERSION}_amd64.deb /binaries/deb/amd64/
COPY datadog-dotnet-apm_${PACKAGE_VERSION}_arm64.deb /binaries/deb/arm64/
COPY datadog-dotnet-apm-${PACKAGE_VERSION}-1.x86_64.rpm /binaries/rpm/amd64/
COPY datadog-dotnet-apm-${PACKAGE_VERSION}-1.aarch64.rpm /binaries/rpm/arm64/

COPY datadog-dotnet-apm-${PACKAGE_VERSION}-musl.tar.gz /binaries/tar/
COPY datadog-dotnet-apm-${PACKAGE_VERSION}.arm64.tar.gz /binaries/tar/

FROM scratch
COPY --from=collect /binaries/* /
