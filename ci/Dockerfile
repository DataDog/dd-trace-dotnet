FROM microsoft/dotnet:latest

RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF \
        && echo "deb http://download.mono-project.com/repo/debian stretch main" >> /etc/apt/sources.list.d/mono-official.list \
        && apt-get update \
        && apt-get install -y --no-install-recommends mono-devel msbuild libuv1-dev \
        && rm -rf /var/lib/apt/lists/*

ENV FrameworkPathOverride="/usr/lib/mono/4.5/"
