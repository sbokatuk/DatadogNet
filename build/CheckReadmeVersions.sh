#!/bin/sh
# Fails when README.md names a package version that is not the one this repository currently
# builds. The install snippets are copy-paste starting points, and a hardcoded version there goes
# stale silently on every release - 3.14.0.1 sat in the snippets while 3.14.0.3 shipped. Running
# this in CI makes the version bump before a release drag the README along with it.
#
# What is checked:
#   - every <PackageReference Include="DatadogNet..." Version="..."> pin. The suffix routes the
#     expectation: .iOS/.Android/.Mac pins are binding packages with their own release line,
#     everything else is this repository's own set at $(VersionPrefix).
#   - the device-check examples (run-simulator-tests.sh / run-emulator-tests.sh <version> ...).
#   - the architecture diagram's "DatadogNet.iOS <v>" / ".Android <v>" / ".Mac <v>" mentions.
#   - the Version map row: the one table stating the whole stack must exist and must carry
#     exactly the versions the props build against.
# Prose that explains the version *scheme* ("3.14.0.1 is dd-sdk-ios 3.14.0, binding revision 1")
# is deliberately not checked - it describes the format, not the current release.
set -eu

root="$(cd "$(dirname "$0")/.." && pwd)"
readme="$root/README.md"
props="$root/Directory.Build.props"

prop() {
  sed -n "s/.*<$1>\(.*\)<\/$1>.*/\1/p" "$props" | head -1
}

version="$(prop DatadogNativeVersion).$(prop DatadogBindingRevision)"
ios_version="$(prop DatadogNetiOSVersion)"
android_version="$(prop DatadogNetAndroidVersion)"
mac_version="$(prop DatadogNetMacVersion)"

bad=0

old_ifs=$IFS
IFS='
'
for pin in $(grep -oE 'Include="DatadogNet[^"]*" +Version="[0-9][^"]*"' "$readme"); do
  id=$(printf '%s' "$pin" | sed -E 's/Include="([^"]*)".*/\1/')
  ver=$(printf '%s' "$pin" | sed -E 's/.*Version="([^"]*)"/\1/')
  case "$id" in
    *.iOS)     expected="$ios_version" ;;
    *.Android) expected="$android_version" ;;
    *.Mac)     expected="$mac_version" ;;
    *)         expected="$version" ;;
  esac
  if [ "$ver" != "$expected" ]; then
    echo "README pins $id $ver, but the current version is $expected" >&2
    bad=1
  fi
done

for token in $(grep -oE 'run-(simulator|emulator)-tests\.sh +[0-9][0-9.]*' "$readme" | grep -oE '[0-9][0-9.]*$'); do
  if [ "$token" != "$version" ]; then
    echo "README runs the device checks at $token, but the current version is $version" >&2
    bad=1
  fi
done

for mention in $(grep -oE 'DatadogNet\.(iOS|Android|Mac) +[0-9][0-9.]*' "$readme"); do
  repo=$(printf '%s' "$mention" | grep -oE 'iOS|Android|Mac')
  ver=$(printf '%s' "$mention" | grep -oE '[0-9][0-9.]*$')
  case "$repo" in
    iOS)     expected="$ios_version" ;;
    Android) expected="$android_version" ;;
    Mac)     expected="$mac_version" ;;
  esac
  if [ "$ver" != "$expected" ]; then
    echo "README shows DatadogNet.$repo $ver, but the pinned binding version is $expected" >&2
    bad=1
  fi
done
IFS=$old_ifs

# The Version map row. Built from the same props the packages are, so a version bump that forgets
# the table fails here rather than shipping a matrix that contradicts the packages.
native_version="$(prop DatadogNativeVersion)"
android_native_version="$(prop DatadogAndroidNativeVersion)"
map_row="| $version | $native_version | $android_native_version | $ios_version | $android_version | $mac_version |"
if ! grep -qF "$map_row" "$readme"; then
  echo "README's Version map row is missing or stale - expected a row starting:" >&2
  echo "  $map_row" >&2
  bad=1
fi

if [ "$bad" -ne 0 ]; then
  echo "CheckReadmeVersions: README.md is stale - update the versions above (current: $version)" >&2
  exit 1
fi

echo "CheckReadmeVersions: README.md agrees with Directory.Build.props ($version)"
