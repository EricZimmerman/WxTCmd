# WxTCmd

Parses the Windows 10/11 Timeline database, `ActivitiesCache.db`: each activity's executable, display text, content
(file or URI), clipboard text, start and end times and the device it came from, plus the activity operations and
package IDs. Results are exported to CSV.

| | |
|---|---|
| **Version** | 2026.10.1 |
| **Download** | [.NET 10](https://download.ericzimmermanstools.com/net/WxTCmd.zip) |
| **All tools** | [ericzimmerman.github.io](https://ericzimmerman.github.io) or [Get-ZimmermanTools](https://download.ericzimmermanstools.com/Get-ZimmermanTools.zip), which downloads them all and keeps them current |
| **Changes** | [ChangeLog](https://ericzimmerman.github.io/changelog/) |
| **Issues** | [EricZimmerman/WxTCmd](https://github.com/EricZimmerman/WxTCmd/issues) or [EricZimmerman/Issues](https://github.com/EricZimmerman/Issues/issues) |

## Requirements

- The [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Run `WxTCmd.exe` on Windows, or
  `dotnet WxTCmd.dll` on any system with the runtime.
- The native SQLite library ships next to it, one file per system: `e_sqlite3.dll` (Windows x64), `libe_sqlite3.so`
  (Linux x64) and `libe_sqlite3.dylib` (macOS on Apple silicon). For other systems, and for problems on Linux and
  macOS, see [LINUX.md](LINUX.md).

## Command line

```text
Usage:
  WxTCmd [options]

Options:
  -f <f>          File to process
  --csv <csv>     Directory to save CSV formatted results to. Be sure to include the full path in double quotes
  --dt <dt>       The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options [default: yyyy-MM-dd HH:mm:ss]
  --temp <temp>   Folder for the temporary copies of the SQLite databases, which are opened instead of the evidence and deleted when done. Default: a Temp folder next to WxTCmd. A folder that isn't there is created, and removed again when done. If that folder can't be written to, WxTCmd stops: give a writable folder with --temp
  --debug         Show debug information during processing
  --trace         Show trace information during processing
  --co            When true, save all console messages to <timestamp>_WxTCmd_<version>_ConsoleLog.txt in the --csv directory. Use --co false to turn it off
  -?, -h, --help  Show help and usage information
  --version       Show version information
```

Short options (a single letter) start with one dash, long options with two.

## Examples

```text
WxTCmd.exe -f "C:\Users\eric\AppData\Local\ConnectedDevicesPlatform\L.eric\ActivitiesCache.db" --csv c:\temp
```

Database files are typically found at
`C:\Users\<profile>\AppData\Local\ConnectedDevicesPlatform\L.<profile>\ActivitiesCache.db`.

## Output

One CSV per table with rows: `<timestamp>_<user>_Activity.csv`, `_Activity_PackageIDs.csv` and
`_ActivityOperations.csv` (the user name comes from a `Users` folder in the path; the timestamp is the UTC time the run
started, the same as the console log's). Times are UTC.

- The database is copied (with its `-wal` and `-journal`) to a temporary folder and only the copy is read, so the
  evidence is not changed. That folder is `--temp`, or by default a `Temp` folder next to WxTCmd (never the user's
  `%TEMP%`); the copy is deleted when done. If the folder can't be written to, WxTCmd stops and asks for `--temp`. Rows
  that are only in the `-wal` (all of them, in some Windows 11 databases) are included, so keep the `-wal` next to the
  database.
- `ClipboardText` is the text in `ClipboardPayload` (clipboard history synced to the cloud, activity type 10), decoded
  from base64.
- A value that can't be read is listed in the console and the rest of the row is kept. A damaged database gives the
  rows that could be read.
- With `--co` (on by default; `--co false` turns it off), everything written to the console is also saved, with times and levels, to
  `<yyyyMMddHHmmss>_WxTCmd_<version>_ConsoleLog.txt` in the `--csv` directory (the time in the name is UTC).

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. Damaged databases, files that aren't timeline databases, and tables or values that can't be read are reported but are not a failure |
| `1` | The tool failed: invalid arguments, a missing `-f` file, a database it can't read or copy, a `--temp` folder or CSVs that can't be written, or an unexpected error |

## What's new in 2026.10

- The evidence is no longer changed: the database was opened in place, which wrote the `-wal` into
  `ActivitiesCache.db` and deleted the `-wal` and `-shm` files. Only a copy is read now; rows that are only in the
  `-wal` are still included.
- Text in other languages is kept (payloads were read as ASCII, so Cyrillic, Japanese and other text became `???`).
- Windows 10 1803 databases are read (the whole Activity table was lost); one bad row or value no longer loses its
  table, and a damaged or truncated database gives the rows that could be read.
- `ContentInfo` shows the file's URI as it is instead of cutting it at fixed places.
- New columns at the end: `AppActivityId` (PR #7, Issue #6) and `ClipboardText` (Issue #9).
- No longer writes `SQLite.Interop.dll` / `libSQLite.Interop.so` into the folder it is run from: the SQLite library
  ships next to it, so Linux and macOS work without building one.
- New `--temp`. Exit code 1 when the tool fails (it was always 0). Error reports include source file and line numbers.
- New `--co` (on by default; `--co false` turns it off): saves all console messages to `<timestamp>_WxTCmd_<version>_ConsoleLog.txt` in the `--csv` directory
  (Issues #259). CSV names use the UTC time, like the console log and the other tools (they used local time). --debug
  and --trace show the time of each message again (they showed the --dt date) (2026.10.1)

The full list is in the [ChangeLog](https://ericzimmerman.github.io/changelog/).

## Building

```text
dotnet build WxTCmd.slnx -c Release
```

Builds `net10.0`. To build for one system (Linux arm64, Alpine, Intel Macs), see [LINUX.md](LINUX.md).

## Further reading

- [Introducing WxTCmd!](https://binaryforay.blogspot.com/2018/05/introducing-wxtcmd.html)

## Special thanks

Open source development funding and support provided by the [SANS Institute](https://www.sans.org/) and
[SANS DFIR](https://www.sans.org/digital-forensics-incident-response/).

## License

[MIT](LICENSE), Copyright (c) Eric R. Zimmerman 2027.
