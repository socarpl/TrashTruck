# TrashTruck

Windows utility for listing and emptying the current user's Recycle Bins.
Run `TrashTruck.exe --help` for options.

## Standalone Native AOT build

Requires the .NET 9 SDK and Visual Studio C++ build tools with the Windows SDK.
The published executable does not require an installed .NET runtime.

```powershell
dotnet publish TrashTruck.csproj -c Release -o artifacts/win-x64
```

Distribute `artifacts/win-x64/TrashTruck.exe`. The PDB is optional debug information.

## Automatic versioning

Assembly, file, product, and displayed versions use `1.0.0.<build>`.
Each build or publish that runs the build increments `BuildNumber.txt` once.
Design-time builds and `publish --no-build` do not increment it. A failed build
may consume a number. Clean does not reset it.

Keep `BuildNumber.txt` with the project to preserve the sequence, and run builds
sequentially within a checkout. Change `VersionPrefix` in `TrashTruck.csproj` to
release a new base version; reset the counter to `0` when starting its sequence.

## GitHub automatic releases

Upload the source to a GitHub repository, including the hidden `.github` folder,
`TrashTruck.csproj`, `TrashTruck.sln`, `Program.cs`, and `BuildNumber.txt`.
Do not upload `bin`, `obj`, or `artifacts`.

The workflow in `.github/workflows/release.yml` runs on pushes to the repository's
default branch. You can also run **Build and release TrashTruck** manually from
the **Actions** tab, selecting the default branch.

It publishes and checks a Windows x64 Native AOT executable, then creates or
updates a single release tagged `latest`. The existing `TrashTruck.exe` asset is
replaced with the new build; it does not create a separate release per version.
Other releases you create manually are not removed.

Download it from **Releases**, or use this permanent path on your repository:

```text
https://github.com/OWNER/REPOSITORY/releases/latest/download/TrashTruck.exe
```

GitHub supplies the authentication token automatically. The workflow requests
`contents: write`; no personal token or extra secret is needed. Actions must be
enabled and repository/organization policy must allow this permission. Keep
release immutability disabled for this rolling release, and allow the workflow
to update the `latest` tag if you configure tag protection.

CI versions use `1.0.0.<tracked counter + workflow run number>` without committing
counter changes. Re-running the same workflow run reuses its version. Local
builds continue to increment `BuildNumber.txt` normally.
