FROM ubuntu:18.04

RUN apt-get update && apt-get install -y \
    build-essential \
    rpm \
    ruby \
    ruby-dev \
    rubygems \
    git

RUN gem install --no-ri --no-rdoc fpm
