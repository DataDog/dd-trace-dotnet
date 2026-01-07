# To update and deploy this image, see UPDATING_IMAGE.md
#
# To build this file locally, starting from the root directory:
# cd tracer/build/_build/docker/gitlab
# docker build -f gitlab.windows.dockerfile --tag datadog/dd-trace-dotnet-docker-build:dotnet10 .
# docker push datadog/dd-trace-dotnet-docker-build:dotnet10

ARG BASE_IMAGE=mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
FROM ${BASE_IMAGE}
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

USER ContainerAdministrator

# VS Build tool link found from https://learn.microsoft.com/en-gb/visualstudio/releases/2022/release-history#release-dates-and-build-numbers
# You can grab the SHA for the downloaded file using (Get-FileHash -Algorithm SHA256 $out).Hash
ENV VSBUILDTOOLS_VERSION="17.14.36310.24" \
    VSBUILDTOOLS_SHA256="A783199025439D65F310BFF041E278B966A6DBED8DBCD7FC96B55389F574EF41" \
    VSBUILDTOOLS_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/ae7ac791-9759-4076-bba7-47ff510c57af/a783199025439d65f310bff041e278b966a6dbed8dbcd7fc96b55389f574ef41/vs_BuildTools.exe" \
    VSBUILDTOOLS_INSTALL_ROOT="c:\devtools\vstudio"

# Install VS
COPY install_vstudio.ps1 .
RUN powershell -Command .\install_vstudio.ps1 -Version $ENV:VSBUILDTOOLS_VERSION -Sha256 $ENV:VSBUILDTOOLS_SHA256 -InstallRoot $ENV:VSBUILDTOOLS_INSTALL_ROOT $ENV:VSBUILDTOOLS_DOWNLOAD_URL

# Install WIX
ENV WIX_VERSION="3.11.2" \
    WIX_SHA256="32bb76c478fcb356671d4aaf006ad81ca93eea32c22a9401b168fc7471feccd2"
COPY install_net35.ps1 .
RUN Powershell -Command .\install_net35.ps1

COPY install_wix.ps1 .
RUN powershell -Command .\install_wix.ps1 -Version $ENV:WIX_VERSION -Sha256 $ENV:WIX_SHA256

# Install .NET 10
# To find these links, visit https://dotnet.microsoft.com/en-us/download, click the Windows, x64 installer, and grab the download url + SHA512 hash
ENV DOTNET_VERSION="10.0.100" \
    DOTNET_DOWNLOAD_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.100/dotnet-sdk-10.0.100-win-x64.exe" \
    DOTNET_SHA512="e9920ce4b9b2fa3ce63a35f288080bb8d2b7f5bfbf2d51588276f81eddc8858254760f172aa1d0a7211a98378816c6e8bb17b59f4844db8456988ad10a557ca9"

COPY install_dotnet.ps1 .
RUN powershell -Command .\install_dotnet.ps1  -Version $ENV:DOTNET_VERSION -Sha512 $ENV:DOTNET_SHA512 $ENV:DOTNET_DOWNLOAD_URL

# Copy the CI Identities GitLab Job Client
COPY ci-identities-gitlab-job-client-windows-amd64.exe c:/devtools/ci-identities-gitlab-job-client.exe

# Java and code signing tool environment variables
ENV JAVA_VERSION "17.0.8"
ENV JAVA_SHA256 "db6e7e7506296b8a2338f6047fdc94bf4bbc147b7a3574d9a035c3271ae1a92b"
ENV WINSIGN_VERSION "v0.5.0"

# Install JAVA
COPY helpers.ps1 install_java.ps1 ./
RUN powershell -Command .\install_java.ps1

# Install Windows Code Signer
COPY --from=registry.ddbuild.io/windows-code-signer/go:$WINSIGN_VERSION c:/windows-code-signer/windows-code-signer.exe c:/devtools/windows-code-signer.exe

# Copy everything else
COPY . .
ENTRYPOINT ["/entrypoint.bat"]
