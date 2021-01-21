FROM ubuntu:20.04

RUN DEBIAN_FRONTEND=noninteractive apt-get update && apt-get install -y --fix-missing \
    build-essential \
    rpm \
    ruby \
    ruby-dev \
    rubygems \
    git

RUN gem install --no-ri --no-rdoc fpm
