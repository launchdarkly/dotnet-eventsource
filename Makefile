TEMP_TEST_OUTPUT=/tmp/sse-contract-test-service.log

build-contract-tests:
	@cd contract-tests && dotnet build TestService.csproj

start-contract-test-service:
	@cd contract-tests && dotnet run --project TestService.csproj --no-build

start-contract-test-service-bg:
	@echo "Test service output will be captured in $(TEMP_TEST_OUTPUT)"
	@make start-contract-test-service >$(TEMP_TEST_OUTPUT) 2>&1 &

run-contract-tests:
	@curl -s https://raw.githubusercontent.com/launchdarkly/sse-contract-tests/main/downloader/run.sh \
      | VERSION=v2 PARAMS="-url http://localhost:8000 -stop-service-at-end \
          -skip 'basic parsing/ID field is ignored if it contains a null' \
          -skip 'linefeeds/CR separator' \
          -skip 'linefeeds/CRLF where CR is end of 1 chunk' \
          -skip 'reconnection/discards partial messages on retry'" sh

contract-tests: build-contract-tests start-contract-test-service-bg run-contract-tests

.PHONY: build-contract-tests start-contract-test-service start-contract-test-service-bg run-contract-tests contract-tests
