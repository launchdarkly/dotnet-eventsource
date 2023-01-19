#!/bin/bash

set -e

TESTFRAMEWORK=${TESTFRAMEWORK:-netcoreapp2.1}
TEMP_TEST_OUTPUT=/tmp/sse-contract-test-service.log

cd $(dirname $0)

BUILDFRAMEWORK=netstandard2.0 dotnet build TestService.csproj
dotnet bin/Debug/${TESTFRAMEWORK}/ContractTestService.dll >${TEMP_TEST_OUTPUT} &
curl -s https://raw.githubusercontent.com/launchdarkly/sse-contract-tests/v0.0.3/downloader/run.sh \
  | VERSION=v0 PARAMS="-url http://localhost:8000 -debug -stop-service-at-end" sh || \
  (echo "Tests failed; see ${TEMP_TEST_OUTPUT} for test service log"; exit 1)
