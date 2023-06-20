ARG BASE_IMAGE=mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
FROM ${BASE_IMAGE}
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# VS Build tool link found from https://learn.microsoft.com/en-gb/visualstudio/releases/2022/release-history#release-dates-and-build-numbers
ENV DOTNET_VERSION="7.0.101" \
    DOTNET_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/35660869-0942-4c5d-8692-6e0d4040137a/4921a36b578d8358dac4c27598519832/dotnet-sdk-7.0.101-win-x64.exe" \
    DOTNET_SHA512="51776ee364ef9c79feaa7b7c970bb59a3f26344797156aefad13271e5e8b720c9746091bfc7add83c80752c40456491d062c54ae2d1ed5b0426be381a0aa980a" \
    VSBUILDTOOLS_VERSION="17.4.33110.190" \
    VSBUILDTOOLS_SHA256="FABDA7E422ADA90C229262A4447C08933EC5BF66A9F38129CD19490EEA2DD180" \
    VSBUILDTOOLS_DOWNLOAD_URL="https://download.visualstudio.microsoft.com/download/pr/2160190b-bb01-4670-9492-34da461fa0c9/fabda7e422ada90c229262a4447c08933ec5bf66a9f38129cd19490eea2dd180/vs_BuildTools.exe" \
    VSBUILDTOOLS_INSTALL_ROOT="c:\devtools\vstudio" \
    WIX_VERSION="3.11.2" \
    WIX_SHA256="32bb76c478fcb356671d4aaf006ad81ca93eea32c22a9401b168fc7471feccd2" \
    JAVA_VERSION="21-ea+26" \
    JAVA_SHA256="1dc7aa62631ee8f9e91346529b41bb40764c9f0dd9192b24e52a1c19ce3cda65" \
    WINSIGN_VERSION="0.1.0" \
    WINSIGN_SHA256="44f5c3bb3baed13ebd65e5a5386501ee2b4a18afe51fbca93b951523ea6999ec" \
    PYTHON_VERSION="3.9.13" \
    PYTHON_SHA256="fb3d0466f3754752ca7fd839a09ffe53375ff2c981279fd4bc23a005458f7f5d"


USER ContainerAdministrator

# Install VS
COPY install_vstudio.ps1 .
RUN powershell -Command .\install_vstudio.ps1 -Version $ENV:VSBUILDTOOLS_VERSION -Sha256 $ENV:VSBUILDTOOLS_SHA256 -InstallRoot $ENV:VSBUILDTOOLS_INSTALL_ROOT $ENV:VSBUILDTOOLS_DOWNLOAD_URL

# Install WIX
COPY install_net35.ps1 .
RUN Powershell -Command .\install_net35.ps1

COPY install_wix.ps1 .
RUN powershell -Command .\install_wix.ps1 -Version $ENV:WIX_VERSION -Sha256 $ENV:WIX_SHA256

# Install .NET 7
COPY install_dotnet.ps1 .
RUN powershell -Command .\install_dotnet.ps1  -Version $ENV:DOTNET_VERSION -Sha512 $ENV:DOTNET_SHA512 $ENV:DOTNET_DOWNLOAD_URL

## the following two scripts install the tools necessary for codesigning
# python installs a python environment, necessary for the signing tool
# java installs the correct version of openjdk
# winsign installs the required python package

COPY helpers.ps1 .
COPY requirements.txt .
COPY install_python.ps1 .
RUN powershell -Command .\install_python.ps1 -Version $ENV:PYTHON_VERSION -Sha256 $ENV:PYTHON_SHA256

COPY install_java.ps1 .
RUN powershell -Command .\install_java.ps1

COPy install_winsign.ps1 .
RUN powershell -Command .\install_winsign.ps1

# Copy everything else
COPY . .
ENTRYPOINT ["/entrypoint.bat"]
