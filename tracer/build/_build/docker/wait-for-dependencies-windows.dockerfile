FROM mcr.microsoft.com/windows/servercore:ltsc2019-amd64
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /app

ADD wait-for-dependencies.ps1 .

ENTRYPOINT ["powershell.exe", ".\\wait-for-dependencies.ps1"]