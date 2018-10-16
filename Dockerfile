FROM ubuntu:14.04

RUN apt-get update && \
    apt-get install -y \
        git \
        wget

RUN echo "deb http://llvm.org/apt/trusty/ llvm-toolchain-trusty-3.9 main" | sudo tee /etc/apt/sources.list.d/llvm.list
RUN wget -O - http://llvm.org/apt/llvm-snapshot.gpg.key | sudo apt-key add -
RUN sudo apt-get update

RUN sudo apt-get install -y \
    cmake \
    llvm-3.9 \
    clang-3.9 \
    lldb-3.9 \
    liblldb-3.9-dev \
    libunwind8 \
    libunwind8-dev \
    gettext \
    libicu-dev \
    liblttng-ust-dev \
    libcurl4-openssl-dev \
    libssl-dev \
    libnuma-dev \
    libkrb5-dev 

RUN cd /usr/lib/llvm-3.9/lib && ln -s ../../x86_64-linux-gnu/liblldb-3.9.so.1 liblldb-3.9.so.1

RUN mkdir -p /opt
RUN cd /opt && git clone --branch v2.1.5 https://github.com/dotnet/coreclr.git

RUN cd /opt/coreclr && ./build.sh -configureonly
RUN cd /opt/coreclr && ./build.sh -skipconfigure -skipmscorlib -skiptests -skipnuget -skiprestoreoptdata -skipcrossgen -skiprestore -disableoss -cross -crosscomponent

RUN apt-get update && apt-get install -y \
    python-software-properties \
    software-properties-common

RUN add-apt-repository ppa:ubuntu-toolchain-r/test && \
    apt-get update && \
    apt-get install -y \
        curl \
        ninja-build

# cmake
RUN apt-get remove -y cmake && \
    curl -o /tmp/cmake.sh https://cmake.org/files/v3.12/cmake-3.12.3-Linux-x86_64.sh && \
    sh /tmp/cmake.sh --prefix=/usr/local --exclude-subdir --skip-license

# libraries

ENV CXX=clang++-3.9
ENV CC=clang-3.9

# - fmt
RUN cd /opt && git clone --branch 5.2.1 https://github.com/fmtlib/fmt.git
RUN cd /opt/fmt && cmake -G Ninja . && cmake --build .

# - spdlog
RUN cd /opt && git clone --branch v1.2.0 https://github.com/gabime/spdlog.git
RUN cd /opt/spdlog && cmake -G Ninja . && cmake --build .

# - nlohmann/json
RUN cd /opt && git clone --branch v3.3.0 https://github.com/nlohmann/json.git
RUN cd /opt/json && cmake -G Ninja . && cmake --build .