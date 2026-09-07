# To update and deploy this image, see UPDATING_IMAGE.md
#
# To build this file locally, starting from the root directory:
# cd tracer/build/_build/docker/gitlab
# docker build -f gitlab.windows.dockerfile --tag datadog/dd-trace-dotnet-docker-build:dotnet10 .
# docker push datadog/dd-trace-dotnet-docker-build:dotnet10

# The ASP.NET image is the .NET Framework runtime image with IIS enabled. The
# GitLab build image also runs the full-IIS integration-test slice, matching
# the IIS role available on Azure's Windows test workers.
ARG BASE_IMAGE=mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2022
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

# IIS Express is not included in the Visual Studio Build Tools workload.
ENV IISEXPRESS_VERSION="10.0.2001" \
    IISEXPRESS_SHA256="18304FE8A65E397C65FE77C6E73B0ACB1556E8ED7EC9C94678DD42FA7AC1671F" \
    IISEXPRESS_DOWNLOAD_URL="https://download.microsoft.com/download/c/e/8/ce8d18f5-d4c0-45b5-b531-adecd637a1aa/iisexpress_amd64_en-US.msi"

COPY install_iisexpress.ps1 .
RUN powershell -Command .\install_iisexpress.ps1 -Version $ENV:IISEXPRESS_VERSION -Sha256 $ENV:IISEXPRESS_SHA256 -Url $ENV:IISEXPRESS_DOWNLOAD_URL

# Install SQL Server Express LocalDB for the .NET Framework SQL integration tests.
ENV LOCALDB_VERSION="2022" \
    LOCALDB_SHA256="36E0EC2AC3DD60F496C99CE44722C629209EA7302A2CE9CBFD1E42A73510D7B6" \
    LOCALDB_DOWNLOAD_URL="https://download.microsoft.com/download/5/1/4/5145fe04-4d30-4b85-b0d1-39533663a2f1/SQL2022-SSEI-Expr.exe"

COPY install_localdb.ps1 .
RUN powershell -Command .\install_localdb.ps1 -Version $ENV:LOCALDB_VERSION -Sha256 $ENV:LOCALDB_SHA256 -Url $ENV:LOCALDB_DOWNLOAD_URL

# Install the same pinned Azure Functions Core Tools and Storage Emulator used
# by the Azure Windows integration-test stage. Both are needed by the dedicated
# Azure Functions matrix, and remain isolated inside each ephemeral test container.
ENV AZURE_FUNCTIONS_CORE_TOOLS_VERSION="4.0.6280" \
    AZURE_FUNCTIONS_CORE_TOOLS_DOWNLOAD_URL="https://github.com/Azure/azure-functions-core-tools/releases/download/4.0.6280/func-cli-4.0.6280-x64.msi" \
    AZURE_STORAGE_EMULATOR_VERSION="5.10" \
    AZURE_STORAGE_EMULATOR_DOWNLOAD_URL="https://go.microsoft.com/fwlink/?clcid=0x409&linkid=717179"

COPY install_azure_functions_tools.ps1 install_azure_storage_emulator.ps1 ./
RUN powershell -Command .\install_azure_functions_tools.ps1 -Version $ENV:AZURE_FUNCTIONS_CORE_TOOLS_VERSION -Url $ENV:AZURE_FUNCTIONS_CORE_TOOLS_DOWNLOAD_URL
RUN .\install_azure_storage_emulator.ps1 -Version $ENV:AZURE_STORAGE_EMULATOR_VERSION -Url $ENV:AZURE_STORAGE_EMULATOR_DOWNLOAD_URL

# Install MSMQ for the .NET Framework MSMQ integration tests.
COPY install_msmq.ps1 .
RUN powershell -Command .\install_msmq.ps1

# Install a matched headless Chrome and ChromeDriver for the Selenium CI Visibility tests.
# Versions and download links are published by the Chrome for Testing project.
ENV CHROME_VERSION="151.0.7922.77" \
    CHROME_DOWNLOAD_URL="https://storage.googleapis.com/chrome-for-testing-public/151.0.7922.77/win64/chrome-headless-shell-win64.zip" \
    CHROME_SHA256="24F22654F82ABD4FF7DEFCE511AE84EBCD7A6C4845AEFA516E0C53E3AF615ECD" \
    CHROMEDRIVER_DOWNLOAD_URL="https://storage.googleapis.com/chrome-for-testing-public/151.0.7922.77/win64/chromedriver-win64.zip" \
    CHROMEDRIVER_SHA256="ED5189D0A048FCBC16CCC6ED45B47D4E78A79184A23E2FC12A3F804F3242B7C8"

COPY install_chrome.ps1 .
RUN powershell -Command .\install_chrome.ps1 -Version $ENV:CHROME_VERSION -ChromeUrl $ENV:CHROME_DOWNLOAD_URL -ChromeSha256 $ENV:CHROME_SHA256 -ChromeDriverUrl $ENV:CHROMEDRIVER_DOWNLOAD_URL -ChromeDriverSha256 $ENV:CHROMEDRIVER_SHA256

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
