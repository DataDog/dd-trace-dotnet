# To build this file locally, starting from the root directory:
# cd tracer/build/_build/docker/gitlab
# docker build -f gitlab.windows.dockerfile --tag datadog/dd-trace-dotnet-docker-build:latest .
# docker push datadog/dd-trace-dotnet-docker-build:latest

ARG BASE_IMAGE=mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
FROM ${BASE_IMAGE}
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

USER ContainerAdministrator

# VS Build tool link found from https://learn.microsoft.com/en-gb/visualstudio/releases/2022/release-history#release-dates-and-build-numbers
ENV VSBUILDTOOLS_VERSION="17.11.35327.3" \
    VSBUILDTOOLS_SHA256="471C9A89FA8BA27D356748AE0CF25EB1F362184992DC0BB6E9CCF10178C43C27" \
    VSBUILDTOOLS_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/69e24482-3b48-44d3-af65-51f866a08313/471c9a89fa8ba27d356748ae0cf25eb1f362184992dc0bb6e9ccf10178c43c27/vs_BuildTools.exe" \
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

# Install .NET 9
# To find these links, visit https://dotnet.microsoft.com/en-us/download, click the Windows, x64 installer, and grab the download url + SHA512 hash
ENV DOTNET_VERSION="9.0.100" \
    DOTNET_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/10bb041d-e705-473e-9654-27c0e038f5bd/447c0c10654c2949872fa6154b8c27b5/dotnet-sdk-9.0.100-win-x64.exe" \
    DOTNET_SHA512="a12ee028f7dff8f330dbe1914534d237eb6e19cc105139ce5de69df1b4b07ee3c1a3e396574ca776a452e805052e799df14a348ace50191af514c9dc4705ecf0"

COPY install_dotnet.ps1 .
RUN powershell -Command .\install_dotnet.ps1  -Version $ENV:DOTNET_VERSION -Sha512 $ENV:DOTNET_SHA512 $ENV:DOTNET_DOWNLOAD_URL

# Java and code signing tool environment variables
ENV JAVA_VERSION "17.0.8"
ENV JAVA_SHA256 "db6e7e7506296b8a2338f6047fdc94bf4bbc147b7a3574d9a035c3271ae1a92b"
ENV WINSIGN_VERSION "0.2.3"
ENV WINSIGN_SHA256 "8091cd41e8e91b8a6b2ec8c2031b12ea4ca42897b972f9f46c2be6ae4c9961f7"
ENV PYTHON_VERSION "3.8.2"

# Install Python
COPY install_python3.ps1 .
RUN powershell -Command .\install_python3.ps1 -Version $ENV:PYTHON_VERSION

COPY requirements.txt constraints.txt install_python_packages.ps1 ./
RUN powershell -Command .\install_python_packages.ps1

# Install JAVA
COPY helpers.ps1 install_java.ps1 ./
RUN powershell -Command .\install_java.ps1

# Install 
COPY install_winsign.ps1 .
RUN powershell -Command .\install_winsign.ps1

# Copy everything else
COPY . .
ENTRYPOINT ["/entrypoint.bat"]
