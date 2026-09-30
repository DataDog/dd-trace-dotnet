# To update and deploy this image, see UPDATING_IMAGE.md
#
# To build this file locally, starting from the root directory:
# cd tracer/build/_build/docker/gitlab
# docker build -f gitlab.windows.dockerfile --tag datadog/dd-trace-dotnet-docker-build:dotnet11 .
# docker push datadog/dd-trace-dotnet-docker-build:dotnet11

ARG BASE_IMAGE=mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2022
FROM ${BASE_IMAGE}
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

USER ContainerAdministrator

# VS Build tool link found from https://learn.microsoft.com/en-gb/visualstudio/releases/2026/release-history#release-dates-and-build-numbers
# You can grab the SHA for the downloaded file using (Get-FileHash -Algorithm SHA256 $out).Hash
ENV VSBUILDTOOLS_VERSION="18.10.12210.168" \
    VSBUILDTOOLS_SHA256="160F5E9C319E3408867CAE9DE83F5D8803BDF7C34FCC8463E8FC28286E49D99E" \
    VSBUILDTOOLS_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/7437128c-6580-48ab-9c69-f7452be2ee7f/160f5e9c319e3408867cae9de83f5d8803bdf7c34fcc8463e8fc28286e49d99e/vs_BuildTools.exe" \
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

# Install .NET 11
# To find these links, visit https://dotnet.microsoft.com/en-us/download, click the Windows, x64 installer, and grab the download url + SHA512 hash
ENV DOTNET_VERSION="11.0.100-rc.1.26425.128" \
    DOTNET_DOWNLOAD_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/11.0.100-rc.1.26425.128/dotnet-sdk-11.0.100-rc.1.26425.128-win-x64.exe" \
    DOTNET_SHA512="a17775d1a27dfff2e86ead906808bd9bdcb2ca8305de62d5c83a8d22f43817d6463d29ea86f4f4511e1539767993e5414445bf132fccc4bb0661a432f2357dee"

COPY install_dotnet.ps1 .
RUN powershell -Command .\install_dotnet.ps1  -Version $ENV:DOTNET_VERSION -Sha512 $ENV:DOTNET_SHA512 $ENV:DOTNET_DOWNLOAD_URL

# Copy the CI Identities GitLab Job Client
COPY --from=registry.ddbuild.io/ci-identities/ci-identities-gitlab-job-client:v0.6.3-windows-amd64 C:/ci-identities-gitlab-job-client.exe c:/devtools/ci-identities-gitlab-job-client.exe

# Java and code signing tool environment variables
ENV JAVA_VERSION "25.0.1"
ENV JAVA_SHA256 "d56bed274adb2b16deea2dce3f21718d1b0dcdbe2253bc5cc332b525cbcd1fd1"

# Install JAVA
COPY helpers.ps1 install_java.ps1 ./
RUN powershell -Command .\install_java.ps1

# Install Windows Code Signer
COPY --from=registry.ddbuild.io/windows-code-signer/go:v0.7.0 c:/windows-code-signer/windows-code-signer.exe c:/devtools/windows-code-signer.exe

# Install vcpkg and pre-fetch its helper toolchain. 
# Keep VCPKG_VERSION in sync with the vcpkgVersion constant in
# Build.Steps.cs. See UPDATING_IMAGE.md.
ENV VCPKG_VERSION="2024.11.16" \
    VCPKG_ROOT="C:\vcpkg"

COPY install_vcpkg.ps1 .
RUN powershell -Command .\install_vcpkg.ps1 -Version $ENV:VCPKG_VERSION -InstallRoot $ENV:VCPKG_ROOT

# Copy everything else
COPY . .
ENTRYPOINT ["/entrypoint.bat"]
