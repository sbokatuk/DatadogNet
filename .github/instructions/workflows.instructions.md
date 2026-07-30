---
applyTo: ".github/workflows/*.yml"
---

# Workflows

- `build.yml` is the only build pipeline; `pr.yml` and `release.yml` call it and decide the version.
  Add verification to `build.yml`, not to its callers.
- The `verify` input gates package validation, the sample builds, the Release link checks and both
  e2e suites. Pull requests leave it `true`; releases pass `false` because the tagged commit was
  already verified on its pull request. That is only sound while `release.yml`'s `guard` job proves
  the tag is an ancestor of the default branch — never weaken or skip it.
- `auto-release.yml` is byte-identical across the DatadogNet family. Change it in every repository or
  in none; keep the `--diff-filter=A` (added, not modified) and four-part-version checks intact, and
  keep the `gh workflow run` dispatch, since a tag pushed with `GITHUB_TOKEN` does not fire
  `on: push: tags`.
- Publishing jobs use nuget.org trusted publishing: they need `permissions: id-token: write`,
  `environment: nuget.org`, and `NuGet/login@v1` immediately before `dotnet nuget push` — the issued
  key lasts an hour and each OIDC token is exchangeable once. The only secret is `NUGET_USER`.
- Never list packages in a workflow. Generate every list by reading `build/packages.tsv`.
- Any job that touches the Apple toolchain runs on `macos-15` and must use
  `./.github/actions/select-xcode`; `net10.0-ios26.0` refuses any Xcode outside the 26.0 line.
- Workloads are resolved from the working directory's `global.json`, which pins .NET 9 at the
  repository root. Install and build a net10 band from a scratch directory with its own
  `global.json`, or the build fails with NETSDK1147 for a workload you just installed.
- Emulator jobs run on ubuntu (KVM); keep the disk-space cleanup and the KVM udev rule.
- Keep `permissions:` minimal per job, and keep fork pull requests building while skipping publish.
