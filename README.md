# TrashTruck

![TrashTruck mascot](trashtruck.png)

Windows utility for listing and emptying the current user's Recycle Bins.
Run `TrashTruck.exe --help` for options.

## Usage and help output

TrashTruck is a Windows x64 console app. It operates on the **current user's**
Recycle Bins. Emptying a bin permanently deletes its contents.

Run `TrashTruck.exe --help` to display the following. `<build>` is replaced by
that executable's automatically assigned build number.

```text
TrashTruck 1.0.0.<build> - Empty the current user's Recycle Bin on selected or all drives.

Usage:
  TrashTruck [--list | --all | /y | --version | --help | -h] [--quiet]
  TrashTruck <drive> [/y] [--quiet]

Options:
  --list      List Recycle Bin item counts and used space per drive,
              plus a total for successfully queried drives. Deletes nothing.
  <drive>     Clean only this drive. Accepts c, c:, or C:\ (case-insensitive).
              Asks for y/n confirmation unless /y or --quiet is supplied.
  --all       Empty the Recycle Bin on all drives without confirmation.
  /y          Skip confirmation. Without a drive, targets all drives.
  --quiet     Suppress all output, including errors and summaries, and
              assume yes to all confirmations for the selected operation.
              Without a drive or --list, empties all drives without asking.
              Exit code: 0 = success, 1 = failure. Help is always displayed.
  --help, -h  Show this help message and exit without deleting anything.
  --version   Show the app version and exit without deleting anything.

With no arguments, shows app information and waits for y/n confirmation
before emptying the current user's Recycle Bin on all drives.
--all, /y, and --quiet skip confirmation. Deletion is permanent, with no progress
dialogs or sound.
--list cannot be combined with a drive, --all, or /y.
--all cannot be combined with a drive.
After deletion, shows the drives processed and the estimated number and
size of items removed, based on before/after Recycle Bin measurements.

Examples:
  TrashTruck c:
  TrashTruck C:\ /y
  TrashTruck C:\ --quiet
```

### Parameters explained

| Argument | Behavior |
| --- | --- |
| No arguments | Shows app information, then waits for `y/n` confirmation before emptying Recycle Bins on all drives. |
| `c`, `c:`, or `C:\` | Targets only drive C and asks for confirmation. Drive letters are case-insensitive. |
| `--list` | Shows drive type, Recycle Bin item count, used space, and status for each drive, followed by totals. Does not delete anything. Unavailable or unsupported drives are excluded from totals. |
| `--all` | Empties Recycle Bins on all drives immediately, without confirmation. |
| `/y` | Assumes yes and skips confirmation. With a drive argument, targets that drive; by itself, targets all drives. |
| `--quiet` | Suppresses normal output, errors, and operation summaries, and **assumes yes to all confirmations for the selected operation**. By itself, empties all drives immediately. With `--list`, performs only the read-only listing silently. |
| `--help`, `-h` | Displays help and exits without deleting anything. Help remains visible with `--quiet`. |
| `--version` | Displays the app version and exits without deleting anything. Output is suppressed if `--quiet` is also supplied. |

Only one drive can be specified per operation. Do not combine `--all` with a
drive, or combine `--list` with a drive, `--all`, or `/y`; these combinations
return an error without deleting anything. Help takes priority over other
arguments; otherwise `--version` displays the version and exits.

### Examples

```powershell
# Inspect Recycle Bin usage without deleting anything.
.\TrashTruck.exe --list

# Ask before emptying all drives.
.\TrashTruck.exe

# Ask before emptying only drive C (all three drive formats work).
.\TrashTruck.exe c
.\TrashTruck.exe c:
.\TrashTruck.exe C:\

# Empty only drive C immediately, then show the operation summary.
.\TrashTruck.exe C:\ /y

# Empty all drives immediately, then show the operation summary.
.\TrashTruck.exe --all

# Empty only drive C silently; assumes yes without asking.
.\TrashTruck.exe C:\ --quiet

# Empty all drives silently; assumes yes without asking.
.\TrashTruck.exe --quiet

# Display the version or full help without deleting anything.
.\TrashTruck.exe --version
.\TrashTruck.exe --help
```

At a confirmation prompt, `y` proceeds and `n` cancels; either case is accepted.
Other input repeats the prompt. End of input cancels the operation.

### Operation summary and exit codes

After a deletion attempt, normal mode shows per-drive removed item counts,
removed size, remaining items, and a combined total. Counts include files and
folders. Totals are estimates from before/after measurements, rather than exact
physical disk space reclaimed. Failed measurements are marked and excluded.
`--quiet` suppresses the summary.

The app returns exit code `0` for success, help, version, or user cancellation,
and `1` for invalid arguments or a reported operation failure. Listing returns
`1` if no drive could be queried successfully. Exit codes remain available in
quiet mode (for example, PowerShell's `$LASTEXITCODE`).

## Standalone Native AOT build

Requires the .NET 9 SDK and Visual Studio C++ build tools with the Windows SDK.
The published executable does not require an installed .NET runtime.

```powershell
dotnet publish TrashTruck.csproj -c Release -o artifacts/win-x64
```

Distribute `artifacts/win-x64/TrashTruck.exe`. The PDB is optional debug information. The executable embeds `trashtruck.ico`; no separate icon file is needed when distributing it.

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
`TrashTruck.csproj`, `TrashTruck.sln`, `Program.cs`, `BuildNumber.txt`, `trashtruck.ico`, and `trashtruck.png`.
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
