#!/bin/bash

set -ue

# Standard update-version.sh for .NET projects built on Windows
# (This script runs on the Releaser host, not on the CircleCI Windows host)

PROJECT_FILES=$(find ./src -name "*.csproj")
for project_file in ${PROJECT_FILES}; do
  echo "Setting version in ${project_file} to ${LD_RELEASE_VERSION}"
  temp_file="${project_file}.tmp"
  sed "s#^\( *\)<Version>[^<]*</Version>#\1<Version>${LD_RELEASE_VERSION}</Version>#g" "${project_file}" > "${temp_file}"
  mv "${temp_file}" "${project_file}"
done
