
build:
	dotnet build

test:
	dotnet test

clean:
	dotnet clean

TEMP_TEST_OUTPUT=/tmp/sse-contract-test-service.log
TESTFRAMEWORK ?= netcoreapp2.1

build-contract-tests:
	@cd contract-tests && BUILDFRAMEWORK=netstandard2.0 dotnet build TestService.csproj

start-contract-test-service:
	@cd contract-tests && dotnet bin/Debug/${TESTFRAMEWORK}/ContractTestService.dll

start-contract-test-service-bg:
	@echo "Test service output will be captured in $(TEMP_TEST_OUTPUT)"
	@make start-contract-test-service >$(TEMP_TEST_OUTPUT) 2>&1 &

run-contract-tests:
	@curl -s https://raw.githubusercontent.com/launchdarkly/sse-contract-tests/v1.0.0/downloader/run.sh \
      | VERSION=v1 PARAMS="-url http://localhost:8000 -debug -stop-service-at-end" sh

contract-tests: build-contract-tests start-contract-test-service-bg run-contract-tests

.PHONY: build test clean build-contract-tests start-contract-test-service run-contract-tests contract-tests
