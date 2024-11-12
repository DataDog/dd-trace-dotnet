FROM andrewlockdd/alpine-clang:1.0 as base
ARG DOTNETSDK_VERSION

ENV \
    # Unset ASPNETCORE_URLS from aspnet base image
    ASPNETCORE_URLS= \
    # Do not generate certificate
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    # Do not show first run text
    DOTNET_NOLOGO=true \
    # SDK version
    DOTNET_SDK_VERSION=$DOTNETSDK_VERSION \
    # Disable the invariant mode (set in base image)
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    # Disable LTTng tracing with QUIC
    QUIC_LTTng=0
    
RUN apk update \
        && apk upgrade \
        && apk add --no-cache \
        ca-certificates \
        \
        # .NET Core dependencies
        krb5-libs \
        libgcc \
        libintl \
        libssl1.1 \
        libstdc++ \
        zlib-dev \
        \
        # SDK dependencies
        curl \
        icu-libs \
        \
        # our dependencies
        cmake \
        git \
        bash \
        make \
        alpine-sdk \
        util-linux-dev \
        autoconf \
        libtool \
        automake \
        xz-dev \
        gdb \
        musl-dbg \
        cppcheck \
        build-base \
        libldap \
        # Download and install nfpm
    && apkArch="$(apk --print-arch)" \
    && wget https://github.com/goreleaser/nfpm/releases/download/v2.39.0/nfpm_2.39.0_${apkArch}.apk \
    && apk add --allow-untrusted nfpm_2.39.0_${apkArch}.apk \
    && rm nfpm_2.39.0_${apkArch}.apk

ENV IsAlpine=true \
    DOTNET_ROLL_FORWARD_TO_PRERELEASE=1

# Install the .NET SDK
COPY ./bootstrap/dotnet-install.sh .
RUN ./dotnet-install.sh --version $DOTNETSDK_VERSION --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && dotnet help

FROM base as builder

# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project

FROM base as tester

# Install .NET Core runtimes using install script (don't install 2.1 on ARM64, because it's not available)
COPY ./bootstrap/dotnet-install.sh .
RUN if [ "$(uname -m)" != "aarch64" ]; then \
        ./dotnet-install.sh --runtime aspnetcore --channel 2.1 --install-dir /usr/share/dotnet --no-path; \
    fi \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 3.1 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 5.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 6.0 --install-dir /usr/share/dotnet --no-path \
    && ./dotnet-install.sh --runtime aspnetcore --channel 7.0 --install-dir /usr/share/dotnet --no-path \
    && rm dotnet-install.sh


# Copy the build project in and build it
COPY *.csproj *.props *.targets /build/
RUN dotnet restore /build
COPY . /build
RUN dotnet build /build --no-restore
WORKDIR /project
