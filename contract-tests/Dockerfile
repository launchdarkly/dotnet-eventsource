# Dockerfile for building and running the contract test service.
# This needs to be built from the parent directory rather than from

ARG DOCKER_IMAGE
FROM $DOCKER_IMAGE

COPY . .

ENV BUILDFRAMEWORK=netstandard2.0

WORKDIR ./contract-tests

RUN dotnet build

ENTRYPOINT dotnet bin/Debug/$TESTFRAMEWORK/ContractTestService.dll
