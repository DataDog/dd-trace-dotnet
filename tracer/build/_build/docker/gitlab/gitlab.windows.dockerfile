ARG BASE_IMAGE=mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
FROM ${BASE_IMAGE}
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

USER ContainerAdministrator

# VS Build tool link found from https://learn.microsoft.com/en-gb/visualstudio/releases/2022/release-history#release-dates-and-build-numbers
ENV VSBUILDTOOLS_VERSION="17.4.33110.190" \
    VSBUILDTOOLS_SHA256="FABDA7E422ADA90C229262A4447C08933EC5BF66A9F38129CD19490EEA2DD180" \
    VSBUILDTOOLS_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/2160190b-bb01-4670-9492-34da461fa0c9/fabda7e422ada90c229262a4447c08933ec5bf66a9f38129cd19490eea2dd180/vs_BuildTools.exe" \
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

# Install .NET 7
ENV DOTNET_VERSION="7.0.304" \
    DOTNET_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/2ab1aa68-3e14-401a-b106-833d66fa992b/060457e640f4095acf4723c4593314b6/dotnet-sdk-7.0.304-win-x64.exe" \
    DOTNET_SHA512="648732c79f6276c37a92e211b4c5b6cf84a0a54637c0f85949ced96d31838b43a4dcae905ef70bafbc9edd3542400746fb1e00c4c84679713e97219493a45938"

COPY install_dotnet.ps1 .
RUN powershell -Command .\install_dotnet.ps1  -Version $ENV:DOTNET_VERSION -Sha512 $ENV:DOTNET_SHA512 $ENV:DOTNET_DOWNLOAD_URL

# Java and code signing tool environment variables
ENV JAVA_VERSION "21-ea+26"
ENV JAVA_SHA256 "1dc7aa62631ee8f9e91346529b41bb40764c9f0dd9192b24e52a1c19ce3cda65"
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
