# WxTCmd

WxTCmd parses the Windows 10/11 Timeline database, `ActivitiesCache.db`.

## Command Line Interface

    Description:
      WxTCmd version 2026.10.0

      Author: Eric Zimmerman (saericzimmerman@gmail.com)
      https://github.com/EricZimmerman/WxTCmd

      Examples: WxTCmd.exe -f "C:\Users\eric\AppData\Local\ConnectedDevicesPlatform\L.eric\ActivitiesCache.db" --csv c:\temp

          Database files are typically found at 'C:\Users\<profile>\AppData\Local\ConnectedDevicesPlatform\L.<profile>\ActivitiesCache.db'

          Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes

    Usage:
      WxTCmd [options]

    Options:
      -f <f>          File to process
      --csv <csv>     Directory to save CSV formatted results to. Be sure to include the full path in double quotes
      --dt <dt>       The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options [default: yyyy-MM-dd HH:mm:ss]
      --debug         Show debug information during processing
      --trace         Show trace information during processing
      -?, -h, --help  Show help and usage information
      --version       Show version information

## Output

One CSV per table with rows: `<timestamp>_<user>_Activity.csv`, `_Activity_PackageIDs.csv` and `_ActivityOperations.csv` (the user name comes from a `Users` folder in the path). Times are UTC.

- The database is copied (with its `-wal` and `-journal`) to a temporary folder and only the copy is read, so the evidence is not changed. Rows that are only in the `-wal` (all of them, in some Windows 11 databases) are included, so keep the `-wal` next to the database.
- `ClipboardText` is the text in `ClipboardPayload` (clipboard history synced to the cloud, activity type 10), decoded from base64.
- A value that can't be read is listed in the console and the rest of the row is kept. A damaged database gives the rows that could be read.
- Exit code 0, or 1 for bad arguments, a file that is not a timeline database or could not be read completely, or CSV files that could not be written.

See [LINUX.md](LINUX.md) for Linux and macOS.

## Documentation

[Introducing WxTCmd!](https://binaryforay.blogspot.com/2018/05/introducing-wxtcmd.html)

# Download Eric Zimmerman's Tools

All of Eric Zimmerman's tools can be downloaded [here](https://ericzimmerman.github.io/#!index.md).

# Special Thanks

Open Source Development funding and support provided by the following contributors: [SANS Institute](http://sans.org/) and [SANS DFIR](http://dfir.sans.org/).
