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
# To find these links, visit https://dotnet.microsoft.com/en-us/download, click the Windows, x64 installer, and grab the download url + SHA512 hash
ENV DOTNET_VERSION="7.0.306" \
    DOTNET_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/4c0aaf08-3fa1-4fa0-8435-73b85eee4b32/e8264b3530b03b74b04ecfcf1666fe93/dotnet-sdk-7.0.306-win-x64.exe" \
    DOTNET_SHA512="29f99b73727fd4ddf3d2d7ddde4f4175c291a4626e474c28d804383e7cdca7ff3ccc91ebf421eb9ee3b167744347a1ddf2bf163a4e34d29d923c5885fc1e10a1"

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
