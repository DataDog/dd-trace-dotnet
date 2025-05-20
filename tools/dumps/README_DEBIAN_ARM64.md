How to open a core dump in debian
=================================

1. Download the memory dump and put it on <home>/tools/dumps.
2. Download the linux-monitoring-home-linux-arm64 artifact from azure devops and extract the content to <home>/shared/bin/monitoring-home
3. Download the linux-tracer-symbols-linux-arm64 artifact from azure devops and extract the content to <home>/shared/bin/monitoring-home/linux-arm64
4. Download the linux-universal-symbols-linux-arm64 artifact from azure devops and extract the content to <home>/shared/bin/monitoring-home/linux-arm64
5. Download the linux-profiler-symbols-linux-arm64 artifact from azure devops and extract the content to <home>/shared/bin/monitoring-home/linux-arm64
6. Execute ./start_debian-arm64.sh to build and start a bash session
7. Go to: /project/dumps
8. Run: dotnet-symbol ./<coredumpfile>
9. Run: lldb --core ./<coredumpfile> /usr/share/dotnet/dotnet
