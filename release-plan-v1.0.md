# v1.0 release plan

Plan to get Hst Core, Hst Compression, Hst Imaging and Hst Imaging Pngcs ready for a v1.0 release.
Version 1.0 locks the public API, so the API changes are made before it is released.

Status: `[ ]` todo, `[x]` done, `[-]` decided not to do.

| | Before | After |
|-|--------|-------|
| Build warnings | 0 | 0 |
| Tests (per runtime) | 334 (net10.0 only) | 375 (net10.0 and net8.0) |
| Hst.Core dependencies on net8.0+ | `Microsoft.Windows.Compatibility` | none |

## Must fix before 1.0

### 1. Replace heavy `Microsoft.Windows.Compatibility` dependency in Hst.Core
- [x] Remove `Microsoft.Windows.Compatibility` package reference from `Hst.Core.csproj`.
- [x] Reference `System.Security.Principal.Windows` only for `netstandard2.1` (in-box for net8.0+).
- [x] Verify `OperatingSystem.IsAdministrator()` still builds for all target frameworks.

Verified by packing: Hst.Core nuspec has no dependencies for net8.0, net9.0 and net10.0 and only
`System.Security.Principal.Windows` 5.0.0 for netstandard2.1.

### 2. Fix `ProcessExtensions.RunProcess` / `RunProcessAsync`
- [x] Read stdout and stderr concurrently to avoid deadlock when stderr buffer fills.
- [x] Wait for process to exit before checking exit code.
- [x] Dispose process.
- [x] Honor `CancellationToken` in `RunProcessAsync` (kills process tree on cancellation).
- [x] Add tests: `Hst.Core.Tests/ExtensionTests/GivenProcessExtensions.cs` (9 tests). 6 of them fail
  against the old implementation (deadlock, cancellation, exit code and missing command).

Also: failing to start a command now throws `IOException` (was `Win32Exception`), and the exception message
includes the exit code.

### 3. Consistent archive APIs (Lha and Lzx)
- [x] Async methods use `Async` suffix and accept `CancellationToken`.
- [x] Lha and Lzx archives have the same shape for listing entries, extracting an entry and opening an entry.
- [x] Archives support `leaveOpen` like `LhaWriter`.
- [x] Update readmes and tests. New tests in `Hst.Compression.Tests/GivenArchiveApi.cs` (11 tests).

| Old | New |
|-----|-----|
| `LhaArchive.Entries()` | `LhaArchive.EntriesAsync(token)` |
| `LhaArchive.Extract(entry, stream)` (sync) | `LhaArchive.ExtractAsync(entry, stream, token)` |
| `LhaArchive.OpenEntry(entry)` | `LhaArchive.OpenEntryAsync(entry, token)` |
| `LhaReader.Read()` | `LhaReader.ReadAsync(token)` |
| `LzxArchive.Entries()` | `LzxArchive.EntriesAsync(token)` |
| `LzxArchive.Next()` | `LzxArchive.NextAsync(token)` |
| `LzxArchive.Extract(stream)` | `LzxArchive.ExtractCurrentAsync(stream, token)` |
| - | `LzxArchive.ExtractAsync(entry, stream, token)` (new, same as Lha) |
| `LzxArchive.OpenEntry(entry)` | `LzxArchive.OpenEntryAsync(entry, token)` |
| `new LhaArchive(stream, options)` | `new LhaArchive(stream, options, leaveOpen = false)` |
| `new LzxArchive(stream, options)` | `new LzxArchive(stream, options, leaveOpen = false)` |

Behaviour fixes made along the way:
- `LhaArchive.EntriesAsync` can be called more than once (it used to return no entries the second time).
- `LzxArchive.EntriesAsync` restores the stream position, so it no longer breaks iterating with `NextAsync`.
- `LzxArchive.ExtractCurrentAsync` throws `InvalidOperationException` before `NextAsync` or after the end of
  the archive (it used to throw `NullReferenceException`).
- `LzxArchive` with `null` options uses default options (it used to fail later).
- Archives throw `ObjectDisposedException` after dispose and null-check arguments.

### 4. Decide visibility of `ILhaDecoder` and `LhaDecoderFactory`
- [x] Keep public as an extension point (documented via `LhaOptions.Decoders`). XML docs were already complete.

### 5. `Result` and `Error` use properties instead of public fields
- [x] `Error.Message`, `Result.Error`, `Result<T>.Value` are get-only properties (source compatible).
- [x] Removed public nested `ResultState` enum, only used by a private field. Success is now `Error == null`.
- [x] Added XML docs and tests: `Hst.Core.Tests/ResultTests/GivenResult.cs` (6 tests).

## Worth doing

### 6. Bitmap reader/writer for less than 8 bits per pixel
- [x] Implemented 1 and 4 bits per pixel in `BitmapReader` and `BitmapWriter` (the readme already said they
  were supported).
- [x] Reader throws `NotSupportedException` for unsupported bits per pixel (e.g. 16) and compression (RLE).
- [x] Reader uses full palette size when header total colors is 0.
- [x] Writer limits palette to the maximum number of colors for bits per pixel.
- [x] Fixed `Image.GetPixel` for 24/32 bits per pixel. Rows after the first read the wrong offset, red and
  blue were swapped compared to `SetPixel` and the bitmap reader, and alpha was 0 for 24 bits per pixel (now
  255). `GetPixel` also works for 1 and 4 bits per pixel (used to throw `NotSupportedException`).
- [x] Tests: `GivenLowBitsPerPixelBitmap.cs` and `GivenImageGetPixel.cs` (15 tests).

### 7. Remove dead `src/Hjg.Pngcs` folder
- [x] Removed. It wasn't in the solution, and `Hst.Imaging.Pngcs` uses the `hjg.pngcs` NuGet package.

### 8. Test oldest shipped runtime
- [x] Test projects target net10.0 and net8.0. Set in each test project's `Directory.Build.props`, which
  still said net6.0 and was overridden in every csproj.
- [ ] netstandard2.1 builds are compiled but not tested (would need a .NET Framework or Mono test host).

### 9. Versioning and release
- [x] Bump version to 1.0.0 in `src/Directory.Build.props`.
- [x] Patch number from commit count is kept as the existing convention, so the release workflow publishes
  `1.0.<commit count>`. Change the workflow if exactly `1.0.0` is wanted.
- [x] Release workflow creates a GitHub release and tag `v<version>` with generated release notes, after
  packages are pushed (needs `contents: write`).

### 10. Package metadata
- [ ] Add package icon. Needs an icon image.
- [x] Add release notes: `PackageReleaseNotes` links to the GitHub releases page.

## Before releasing

- [ ] Update callers (e.g. hst-imager) to the renamed archive API from item 3.
- [ ] Commit, push and check that Build and test passes on Windows, macOS and Linux (process tests use
  `powershell` on Windows and `sh` elsewhere).
- [ ] Run Build and release workflow.
