#!/bin/bash

set -ex

DOCKER_IMAGE=${DOCKER_IMAGE:-mcr.microsoft.com/dotnet/core/sdk:2.1-focal}
TESTFRAMEWORK=${TESTFRAMEWORK:-netcoreapp2.1}

cd $(dirname $0)
cd ..  # must build Docker container from project root

echo "Building contract test service..."
docker build --tag testservice -f contract-tests/Dockerfile \
  --build-arg DOCKER_IMAGE=${DOCKER_IMAGE} --build-arg TESTFRAMEWORK=${TESTFRAMEWORK} .
docker run ldcircleci/sse-contract-tests:1 --output-docker-script 1 --url http://testservice:8000 \
  | bash
