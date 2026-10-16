# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

WxTCmd is a command line tool (`net10.0` only, for the native SQLite library) that parses the Windows 10/11 Timeline database, `ActivitiesCache.db`, into CSVs. It is not packed; the user builds, signs and releases it. GUIDs (known folders) are named with the GuidMapping package. Input may be corrupt or hostile.

- `Classes/ActivitiesCache.cs`: `ActivitiesCache.Read(path)` returns `ActivitiesCacheData`: the rows of `Activity`, `ActivityOperation` and `Activity_PackageId`, `MissingTables`, `TableErrors` (a table that could not be read to the end; the rows before are kept), `ParseErrors` (values that could not be read; the row is kept) and `QuickCheck`. It throws `InvalidDataException` for a file that is not SQLite or whose list of tables can't be read.
  - `SqliteCopy`: the database (with `-wal` and `-journal`) is copied to a temporary folder and only the copy is opened (Microsoft.Data.Sqlite). Up to 2026.5 the evidence was opened in place, which checkpointed the WAL into it and deleted the `-wal` and `-shm`. Some Windows 11 databases have every row in the WAL (the Museum's Win11 sample is a 4 KB file).
  - `PRAGMA writable_schema = ON` on the copy, so a truncated database still gives the rows before the damage (otherwise the whole schema is refused as malformed).
  - Columns are read by name, and a missing column reads as null: the schema changed over Windows versions (user_version 18 in 1803 has no `UserActionState`, `IsRead`, `GroupItems`, `OperationExpirationTime`; 29 adds `DdsDeviceId`, `LocalExpirationTime`, `ThrottleReleaseTime`). 2026.5 used OrmLite, which selects every model property; SQLite reads an unknown quoted name as a string literal, so the 1803 Activity table failed as a whole.
  - Times are seconds since 1970 (UTC); 0 is no time (an empty CSV cell). GUIDs are 16-byte blobs in .NET order (`new Guid(bytes)`, Python's `uuid.UUID(bytes_le=...)`).
  - Payloads and ClipboardPayload are UTF-8 (2026.5 decoded them as ASCII, losing non-English text, a regression of PR #3). `Payload` is shown when it is JSON, else `(Binary data)`; `displayText`, `appDisplayName`, `description`, `contentUri`, `devicePlatform` and `userTimezone` come from it (any case).
  - `Executable`: from the AppId JSON, the `windows_win32`/`x_exe_path` application, else `windows_universal`, else the first; a known folder GUID at the start is replaced by its name (unknown GUIDs are left alone).
  - `ContentUri`: URL decoded; a known folder GUID right after `file:` is named. Real ones are `file:///C:/...?VolumeId={...}&ObjectId={...}`, which 2026.5 mangled with fixed offsets.
  - `ClipboardText`: ClipboardPayload is a JSON array of `{"content": base64, "formatName": ...}`; the content of text formats (or entries without a format) that is valid UTF-8, joined with ` | ` (Issue #9; no real sample yet, the format is from Velociraptor's Windows.Forensics.Clipboard).
- `Program.cs`: the CLI (`-f`, `--csv`, `--dt`). CSV names `<yyyyMMddHHmmss>_<user>_Activity.csv`, `_Activity_PackageIDs.csv`, `_ActivityOperations.csv` (user from a `Users` folder in the path). Exit codes: 0, or 1 for bad arguments, not a timeline database, a table error, or a CSV that could not be written. Value errors are listed but don't change the exit code.

## Behavior to keep

- CSV columns: the class maps (`ActivityMap`, `OperationMap`, `PackageIdMap`) pin every column with `Index`: 2026.5's columns in their order, new ones at the end (`AppActivityId` from PR #7 / Issue #6, `ClipboardText`).
- Nothing is written next to the evidence or in the current folder (2026.5 wrote `SQLite.Interop.dll`/`libSQLite.Interop.so` there).

## Commands

The SDK is pinned in `global.json` (.NET 10). `LangVersion 10` for the tool. Tests target `net10.0` and use NUnit 5 + FluentAssertions 7 (pinned to `[7.2.2,8.0.0)`, since 8.x requires a commercial license).

```bash
dotnet build -c Release                                          # WxTCmd.slnx
dotnet test -c Release                                           # CI (test.yml); CorpusTests is [Explicit]
dotnet test -c Release --filter "FullyQualifiedName~CorpusTests" # ../Corpus/WindowsTimeline (or WXTCMD_CORPUS)
WXTCMD_FUZZ_ROUNDS=4000 dotnet test -c Release --filter Name~RandomDamage
dotnet publish WxTCmd/WxTCmd.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

## Tests

- `ReaderTests`: the samples in `TestFiles` (gzipped; DFIR Artifact Museum, MIT: Win11 with every row in the WAL, Win10 with Cyrillic text; plaso, Apache-2.0: the 1803 schema; see the READMEs there), databases made with the real Windows 11 schema (`TestData.Schema`): ActivityOperation rows (no sample has any), clipboard text, content URIs, executables, bad values, not a database, a truncated database, random damage. The evidence is checked unchanged by hash.
- `CliTests`: WxTCmd.dll as a process: CSV names and headers, `--dt`, exit codes, nothing written to the working folder.
- `CorpusTests`: every `ActivitiesCache.db` in `Corpus/WindowsTimeline` next to the repository (or `WXTCMD_CORPUS`): the Museum's six and plaso's. Never copy corpus files into the repo.
- Validation of a change: run the old and new builds over copies of the corpus and compare the CSVs row by row (paired by Id); check values against Python's sqlite3 on a copy.

## Versioning and release

- `<Version>` in the csproj, bumped by hand. The user builds, signs and releases it.
- `.github/workflows/test.yml` builds and tests on pushes to `master` and PRs. Dependabot (`.github/dependabot.yml`) opens monthly PRs for GitHub Actions and NuGet updates.
