# Dockerfile for building and running the contract test service.
# This needs to be built from the parent directory rather than from

ARG DOCKER_IMAGE

FROM $DOCKER_IMAGE

ARG DOCKER_IMAGE
ARG TESTFRAMEWORK

COPY . .

ENV BUILDFRAMEWORK=netstandard2.0

WORKDIR ./contract-tests

RUN dotnet build

RUN mkdir app && mv bin/Debug/$TESTFRAMEWORK/* app

CMD dotnet app/ContractTestService.dll
