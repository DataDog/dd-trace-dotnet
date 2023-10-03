ARG BASE_IMAGE=mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
FROM ${BASE_IMAGE}
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

USER ContainerAdministrator

# VS Build tool link found from https://learn.microsoft.com/en-gb/visualstudio/releases/2022/release-history#release-dates-and-build-numbers
ENV VSBUILDTOOLS_VERSION="17.8.34205.153" \
    VSBUILDTOOLS_SHA256="D50AFBD0141B9F80ECED58F5F7BA6238E61F8C4028CEB60BD29BAC69102AB30B" \
    VSBUILDTOOLS_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/e0a88b40-6301-4c95-9f7c-d60649666b5e/d50afbd0141b9f80eced58f5f7ba6238e61f8c4028ceb60bd29bac69102ab30b/vs_BuildTools.exe" \
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

# Install .NET 8
# To find these links, visit https://dotnet.microsoft.com/en-us/download, click the Windows, x64 installer, and grab the download url + SHA512 hash
ENV DOTNET_VERSION="8.0.100-rc.2.23502.2" \
    DOTNET_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/92e8b771-8624-48a6-9ffc-9fda1f301fb4/85b45cdf39b2a773fbf8d5d71c3d4774/dotnet-sdk-8.0.100-rc.2.23502.2-win-x64.exe" \
    DOTNET_SHA512="f4c4ebdaa684b2f7cee1b0034749dcde2a6c8b9a5e45fe4b6f1ff4c918367b6e59d23ec1f70a2db3a7ec9867f85507615aa0467a35406b08ed925ec5abf4b49a"

COPY install_dotnet.ps1 .
RUN powershell -Command .\install_dotnet.ps1  -Version $ENV:DOTNET_VERSION -Sha512 $ENV:DOTNET_SHA512 $ENV:DOTNET_DOWNLOAD_URL

# Java and code signing tool environment variables
ENV JAVA_VERSION "17.0.8"
ENV JAVA_SHA256 "db6e7e7506296b8a2338f6047fdc94bf4bbc147b7a3574d9a035c3271ae1a92b"
ENV WINSIGN_VERSION "0.2.0"
ENV WINSIGN_SHA256 "760aa4e3bf12b48ba134f72dda815e98ec628f84420d0ef1bdf4b0185b90193a"
ENV PYTHON_VERSION "3.8.2"

# Install Python
COPY install_python3.ps1 .
RUN powershell -Command .\install_python3.ps1 -Version $ENV:PYTHON_VERSION

COPY requirements.txt .
COPY install_python_packages.ps1 .
RUN powershell -Command .\install_python_packages.ps1

# Install JAVA
COPY helpers.ps1 .
COPY install_java.ps1 .
RUN powershell -Command .\install_java.ps1

# Install 
COPY install_winsign.ps1 .
RUN powershell -Command .\install_winsign.ps1

# Copy everything else
COPY . .
ENTRYPOINT ["/entrypoint.bat"]
