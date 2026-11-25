# API Docker build context regression

This is a P03 review correction, not phase approval. P03 remains InReview and the P04 handoff remains Draft. The API Dockerfile, SDK/package versions and pinned seed files are unchanged.

## Cause and correction

The former rules started with `**` but re-included `!backend/`, `!backend/src/` and `!backend/src/**/`. Docker does not treat a trailing slash as a directory-only restriction. These exceptions admitted unrelated descendants. The later `**/appsettings.*.Local.json` rule also misses the exact name `appsettings.Local.json`.

The corrected [.dockerignore](../.dockerignore) has no directory-tree exceptions. It includes the four production projects' C# files, project files and lockfiles, `global.json`, the two reviewed API appsettings files, and the exact three seed JSON files plus SOURCE.md and LICENSE.txt. Final deny rules cover generated directories, local appsettings, environment files, secret files and key/certificate extensions. This uses Docker's matcher, not `.gitignore` assumptions. [Docker's matching documentation](https://docs.docker.com/build/concepts/context/#dockerignore-files).

## Reproduce safely

Requires Git, PowerShell 7, tar and a running Linux Docker engine. This test never starts/stops SQL or Mailpit, invokes Seed, or starts the API. Docker builds consume resources; run them only in the authorized review environment.

```powershell
./docker/test-api-build-context.ps1 -ContextOnly
./docker/test-api-build-context.ps1
```

The first checks the context only. The second also builds the real API Dockerfile and inspects that image's published assembly/files using the pinned SDK in a standalone offline probe. It does not add a diagnostic endpoint or execute application Program.

The fixture is created under `tmp/docker-context-<random>`. `git archive HEAD` supplies clean tracked production blobs; ignored files, modified host settings, secrets and the actual TARGET_SCHEDULE.md are never copied. The current reviewed Dockerfile and rules are then overlaid. This intentionally tests HEAD's production sources; commit source changes before testing their build. The temporary `.git/config`, schedule and local settings are invented test files containing a non-sensitive marker only.

The test first uses `FROM scratch; COPY . /` with Docker's local exporter. It enumerates **all** resulting files, including hidden files on Linux, and compares an exact allowed inventory. It requires every expected input and rejects every extra file, not merely the named secret fixtures. Reports contain public paths/checksums and fake-fixture metadata only.

## Coverage

157 canonical allowed inputs at the reviewed baseline: `global.json`; production `.cs`/`.csproj`/`packages.lock.json`; API `appsettings.json` and `appsettings.Development.json`; the three pinned seed files; SOURCE.md; LICENSE.txt. Counts are computed each run rather than hard-coded assertions.

79 forbidden fixtures: each of the following 24 names at repository root, API project root and nested SeedData level, plus seven additional paths:

- `appsettings.Local.json`, `appsettings.Development.Local.json`, `appsettings.Production.Local.json`, `appsettings.Staging.Local.json`.
- `.env`, `.env.local`, `.env.Development`, `secrets.json`.
- `private.pfx`, `.p12`, `.key`, `.pem`, `.p8`, `.ppk`, `.jks`, `.keystore`, `.pvk`, `.snk`, `.cer`, `.crt`, `.der` (all with the `private` basename).
- `unapproved.json`, `unapproved.txt`, `private.dll`.
- Additional paths: API `bin/Release/Generated.cs` and `obj/Generated.cs`; `backend/tests/PrivateTest.cs`; `docs/TARGET_SCHEDULE.md`; `.git/config`; `.aws/credentials`; `tmp/private.txt`.

The published-image probe checks exactly three `HawalekNow.ReferenceSeed.*` manifest resources and SHA256 of each stream against clean source bytes. It checks the published `Modules/Locations/SeedData/SOURCE.md` and LICENSE.txt hashes. Finally it scans every published file for prohibited filenames and the fake marker in UTF-8/UTF-16. The production Dockerfile copies context only into the build source tree, then only the verified publish tree into `/app` of the runtime image. No forbidden fixture reaches that copy boundary.

## Before/after evidence and CI

The original rules at `11ffdecb0dded7ac71eb86c93f422024d0175289` failed the actual Docker COPY contract: all157 required inputs were present, but31 forbidden fixtures and69 total extra files were admitted. This included API and nested `appsettings.Local.json`, several key/certificate extensions, and a test C# file. These were dummy markers, not evidence of a real secret disclosure. Existing variant-local appsettings and `.env` exclusions working in that run did not compensate for the missing exact filename or broad exceptions.

After correction, actual Docker COPY admitted all157 required files, zero of79 forbidden fixtures and zero extras. Full image/probe and solution/CI results are recorded in [P03 review](../planning/phase-03/PHASE_REVIEW.md) and [evidence](../planning/phase-03/verification.json), including their exact scopes and revisions. Earlier build-only success did not prove secret exclusion; this correction preserves that history and adds the missing regression.

The [existing Docker CI job](../.github/workflows/p01-baseline.yml) runs the full script before the existing startup smoke. It keeps its freshly created API tag for that smoke. Default local runs remove only their randomly tagged/owner-labeled verification images and their owner-checked fixture directory. A pre-existing image tag is never overwritten; no prune, volume cleanup, shared-service restart or host-directory mount is used. BuildKit cache is not pruned.
