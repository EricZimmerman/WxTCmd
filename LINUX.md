# Running WxTCmd on Linux and macOS

WxTCmd runs natively on Linux and macOS. It uses Microsoft.Data.Sqlite, which brings the native SQLite library for each platform, so nothing needs to be compiled or copied any more (before 2026.10.0, a `libSQLite.Interop.so` had to be built and was written to the current folder at run time).

Install the [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/linux), then, from the folder with `WxTCmd.slnx`:

```
dotnet publish WxTCmd/WxTCmd.csproj -c Release -r linux-x64 -p:PublishSingleFile=true -o publish
./publish/WxTCmd -f /path/to/ActivitiesCache.db --csv /path/to/output
```

Use `-r linux-arm64`, `-r osx-arm64` and so on for other platforms.

Thanks to reece394 for the first Linux support (#11).
