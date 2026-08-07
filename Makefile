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

# ---- Android contract tests ----
# Requires: an Android emulator or device connected via adb, and the .NET 9 SDK with the
# android workload installed. See ci.yml's contract-tests-android job for how CI does this.

build-contract-tests-android:
	@cd contract-tests-android && dotnet build ContractTestService.Android.csproj -c Debug

# Installs the built APK, launches the MainActivity, waits for the app to be listening,
# and forwards the local port to the emulator. All adb glue is in scripts/ so the CI
# workflow's script: block can stay a two-line call to make.
start-contract-test-service-android:
	@scripts/start-android-test-service.sh

# -host 10.0.2.2 makes callback URLs the harness gives the test service point at the
# emulator's alias for the host machine (see Android emulator docs). Without this the
# service can't POST callbacks back through adb.
#
# The harness only supports inline -skip; we read the Android suppressions file line
# by line and translate each line into a `-skip 'X'` argument.
ANDROID_SUPPRESSIONS = contract-tests-android/testharness-suppressions.txt

run-contract-tests-android:
	@SKIP_ARGS=""; \
	 while IFS= read -r line; do SKIP_ARGS="$$SKIP_ARGS -skip '$$line'"; done < $(ANDROID_SUPPRESSIONS); \
	 curl $${GITHUB_TOKEN:+ -H "Authorization: Token $${GITHUB_TOKEN}"} \
	   -s https://raw.githubusercontent.com/launchdarkly/sse-contract-tests/main/downloader/run.sh \
	   | VERSION=v2 PARAMS="-url http://localhost:8000 -host 10.0.2.2 -stop-service-at-end $$SKIP_ARGS" sh

contract-tests-android: build-contract-tests-android start-contract-test-service-android run-contract-tests-android

.PHONY: build-contract-tests start-contract-test-service start-contract-test-service-bg run-contract-tests contract-tests \
        build-contract-tests-android start-contract-test-service-android run-contract-tests-android contract-tests-android
