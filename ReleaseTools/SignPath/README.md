# SignPath preparation

Nothing here signs or publishes a release automatically. The owner has selected free options only. Provider acceptance, account setup and policy configuration are pending; see the root `SIGNPATH-APPLICATION.md` and `CODE-SIGNING-POLICY.md`.

`Build-Signing-Candidate.ps1` builds the app, launcher and test projects, checks the common product/version metadata and all pinned Proton files, runs regression/update checks with synthetic local fixtures, and stages exactly two Harbor-owned EXEs. It does not install Harbor, change Windows defaults, use account profiles, or authorize release packaging.

Run on Windows with Visual Studio desktop build tools and the .NET Framework 4.8 targeting pack:

```powershell
./ReleaseTools/SignPath/Build-Signing-Candidate.ps1
```

`harbor-executables.xml` restricts the input ZIP to Harbor's app and launcher and checks product/version metadata. It is an unvalidated provider-review template, not an active SignPath configuration.

`signpath-executables.yml.example` is intentionally inactive and outside `.github/workflows`. It pins the official checkout, artifact-upload and SignPath actions to reviewed release commits and uses a standard GitHub-hosted Windows runner. Activate it only after provider approval and configuration. This also requires the owner's GitHub authorization to permit workflow changes. No signing credentials exist in the repository.

The example covers **executable signing only**. SignPath must approve the second installer stage: validate the signed EXEs, run the full browser suites and native installer fixtures against that exact build, stage the signed files with all required notices, compile the installer and submit that installer under a separately restricted provider policy. Validate every returned signature and timestamp. Never sign upstream EXEs under Harbor's policy.

The final signed build needs new verification receipts, package manifests, checksums and higher-sequence update metadata. The private metadata key stays on the owner's Windows account; it is not uploaded to GitHub. Existing 0.6.0 draft assets remain untouched until a separately validated signed candidate replaces them. Publication remains manual and requires owner review.

Before production approval, also review SignPath's installer privacy-display/opt-out requirements and verify the final certificate chain, timestamp and Harbor's Windows trust checks on a clean PC. The current updater uses cache-only revocation lookup; successful validation on the maintainer's PC does not establish behavior on a new system.

Official references:

- https://signpath.org/terms
- https://docs.signpath.io/trusted-build-systems/github
- https://docs.signpath.io/artifact-configuration/examples
