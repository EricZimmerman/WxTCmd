using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using WxTCmd.Classes;

namespace WxTCmd;

internal class Program
{
    private static string _activeDateTimeFormat;

    private static RootCommand _rootCommand;

    private static readonly string Header =
        $"WxTCmd version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
        "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        "\r\nhttps://github.com/EricZimmerman/WxTCmd";

    private static readonly string Footer =
        @"Examples: WxTCmd.exe -f ""C:\Users\eric\AppData\Local\ConnectedDevicesPlatform\L.eric\ActivitiesCache.db"" --csv c:\temp" +
        "\r\n\t " +
        "\r\n\t" +
        @"    Database files are typically found at 'C:\Users\<profile>\AppData\Local\ConnectedDevicesPlatform\L.<profile>\ActivitiesCache.db'" +
        "\r\n\t" +
        "\r\n\t" +
        "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes";

    private static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task<int> Main(string[] args)
    {
        //https://salt4n6.wordpress.com/2018/05/05/windows-10-timeline-forensic-artefacts/amp/?__twitter_impression=true
        //ActivitiesCache.db

        var fOpt = new Option<string>("-f")
        {
            Description = "File to process"
        };

        var csvOpt = new Option<string>(
            "--csv")
        {
            Description = "Directory to save CSV formatted results to. Be sure to include the full path in double quotes"
        };

        var dtOpt = new Option<string>(
            "--dt")
        {
            Description = "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options",
            DefaultValueFactory = _ => "yyyy-MM-dd HH:mm:ss"
        };

        var tempOpt = new Option<string>("--temp")
        {
            Description = TempFolder.Help
        };

        var debugOpt = new Option<bool>("--debug")
        {
            Description = "Show debug information during processing",
            DefaultValueFactory = _ => false
        };
        var traceOpt = new Option<bool>("--trace")
        {
            Description = "Show trace information during processing",
            DefaultValueFactory = _ => false
        };
        var coOpt = new Option<bool>("--co")
        {
            Description = "Save all console messages to <timestamp>_WxTCmd_<version>_ConsoleLog.txt in the --csv directory",
            DefaultValueFactory = _ => false
        };

        _rootCommand = new RootCommand
        {
            fOpt,
            csvOpt,
            dtOpt,
            tempOpt,
            debugOpt,
            traceOpt,
            coOpt
        };

        _rootCommand.Description = Header + "\r\n\r\n" + Footer;

        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt), result.GetValue(csvOpt), result.GetValue(dtOpt),
            result.GetValue(tempOpt), result.GetValue(debugOpt), result.GetValue(traceOpt), result.GetValue(coOpt)));

        var exitCode = await _rootCommand.Parse(args).InvokeAsync();

        Log.CloseAndFlush();

        return exitCode;
    }

    private class DateTimeOffsetFormatter : IFormatProvider, ICustomFormatter
    {
        private readonly IFormatProvider _innerFormatProvider;

        public DateTimeOffsetFormatter(IFormatProvider innerFormatProvider)
        {
            _innerFormatProvider = innerFormatProvider;
        }

        public object GetFormat(Type formatType)
        {
            return formatType == typeof(ICustomFormatter) ? this : _innerFormatProvider.GetFormat(formatType);
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg is DateTimeOffset dto)
            {
                return dto.ToString(_activeDateTimeFormat);
            }

            if (arg is IFormattable formattable)
            {
                return formattable.ToString(format, _innerFormatProvider);
            }

            return arg?.ToString();
        }
    }

    private static int ShowHelp(string message)
    {
        var help = new CustomHelpAction(new HelpAction());
        help.Invoke(_rootCommand.Parse(message));

        Console.WriteLine();
        return 1;
    }

    private static int DoWork(string f, string csv, string dt, string temp, bool debug, bool trace, bool co)
    {
        var levelSwitch = new LoggingLevelSwitch();

        _activeDateTimeFormat = dt;

        var formatter = new DateTimeOffsetFormatter(CultureInfo.CurrentCulture);

        var template = "{Message:lj}{NewLine}{Exception}";

        if (debug)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        if (trace)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template, formatProvider: formatter)
            .MinimumLevel.ControlledBy(levelSwitch);

        var consoleLog = co ? ConsoleLog.GetPath(DateTimeOffset.UtcNow, csv) : null;
        if (consoleLog != null)
        {
            conf.WriteTo.File(consoleLog, outputTemplate: ConsoleLog.Template, formatProvider: ConsoleLog.Formatter(formatter));
        }

        Log.Logger = conf.CreateLogger();

        if (string.IsNullOrEmpty(f))
        {
            return ShowHelp("-f is required. Exiting");
        }

        if (string.IsNullOrEmpty(csv))
        {
            return ShowHelp("--csv is required. Exiting");
        }

        if (!File.Exists(f))
        {
            return ShowHelp($"File '{f}' not found. Exiting");
        }

        var userProfile = string.Empty;

        try
        {
            userProfile = Regex.Match(f, @"(?:\\|/)Users(?:\\|/)([^\\\/]+)(?!.*(?:\\|/)Users(?:\\|/))", RegexOptions.IgnoreCase).Groups[1].Value;

            if (userProfile.Length > 0)
            {
                userProfile = $"_{userProfile}";
            }
        }
        catch (ArgumentException)
        {
            // Syntax error in the regular expression
        }

        Log.Information("{Header}", Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}", string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
        Console.WriteLine();

        if (consoleLog != null)
        {
            Log.Information("Console log: {Path}", consoleLog);
            Console.WriteLine();
        }
        else if (co)
        {
            Log.Warning("--co: no output directory was given, so the console messages are not saved");
            Console.WriteLine();
        }

        if (IsAdministrator() == false)
        {
            Log.Warning("Warning: Administrator privileges not found!");
            Console.WriteLine();
        }

        //the database is read from a copy, made in --temp or Temp next to WxTCmd (never the user's %TEMP%)
        var tempFolder = TempFolder.Prepare(temp, out var tempCreated, out var tempError);
        if (tempFolder == null)
        {
            Log.Error("The folder for the temporary copy of the database can't be written to ({Error}). Give one with --temp. Exiting", tempError);
            Console.WriteLine();
            return 1;
        }

        var sw1 = new Stopwatch();
        sw1.Start();

        ActivitiesCacheData data;

        try
        {
            data = ActivitiesCache.Read(f, tempFolder);
        }
        catch (InvalidDataException e)
        {
            Log.Error("Error processing database: '{F}': {Message}", f, e.Message);
            Console.WriteLine();
            //not a SQLite database, or too damaged to read: a bad file, not the tool's fault (exit code 0)
            return 0;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error processing database: {Message}", e.Message);
            Console.WriteLine();
            //a damaged database is 0; a file that can't be read or copied (access denied, in use, IO errors) or an
            //unexpected error is 1
            return e is Microsoft.Data.Sqlite.SqliteException ? 0 : 1;
        }
        finally
        {
            TempFolder.RemoveIfEmpty(tempFolder, tempCreated);
        }

        if (data.QuickCheck != "ok")
        {
            Log.Warning("The database is damaged ({Problem}). What could be read is listed", data.QuickCheck.Split('\n')[0]);
        }

        if (data.HasTimelineTables == false)
        {
            Log.Error("'{F}' has none of the Activity, ActivityOperation and Activity_PackageId tables. Is it an ActivitiesCache.db?", f);
            Console.WriteLine();
            //nothing to do (exit code 0)
            return 0;
        }

        foreach (var table in ActivitiesCache.Tables)
        {
            if (data.MissingTables.Contains(table))
            {
                Log.Warning("{Table} table does not exist!", table);
                continue;
            }

            var count = table switch
            {
                "ActivityOperation" => data.Operations.Count,
                "Activity_PackageId" => data.PackageIds.Count,
                _ => data.Activities.Count
            };
            Log.Information("{Table} entries found: {Count:N0}", table, count);
        }

        foreach (var error in data.TableErrors)
        {
            //a damaged table: listed, what was read is written (exit code 0)
            Log.Error("Error processing {Error}", error);
        }

        if (data.ParseErrors.Count > 0)
        {
            Console.WriteLine();
            Log.Warning("Values that could not be read (the rows are listed with the rest):");
            foreach (var error in data.ParseErrors)
            {
                Log.Warning("  {Error}", error);
            }
        }

        try
        {
            if (Directory.Exists(csv) == false)
            {
                Directory.CreateDirectory(csv);
            }

            var ts1 = DateTime.Now.ToString("yyyyMMddHHmmss");

            if (data.Operations.Count > 0)
            {
                WriteCsv(Path.Combine(csv, $"{ts1}{userProfile}_ActivityOperations.csv"), data.Operations, OperationMap(dt));
            }

            if (data.PackageIds.Count > 0)
            {
                WriteCsv(Path.Combine(csv, $"{ts1}{userProfile}_Activity_PackageIDs.csv"), data.PackageIds, PackageIdMap(dt));
            }

            if (data.Activities.Count > 0)
            {
                WriteCsv(Path.Combine(csv, $"{ts1}{userProfile}_Activity.csv"), data.Activities, ActivityMap(dt));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "There was an error saving to '{Csv}': {Message}", csv, ex.Message);
            Console.WriteLine();
            return 1;
        }

        sw1.Stop();

        Console.WriteLine();
        Log.Information("Results saved to: {Csv}", csv);

        Console.WriteLine();
        Log.Information("Processing complete in {TotalSeconds:N4} seconds", sw1.Elapsed.TotalSeconds);
        Console.WriteLine();

        return 0;
    }

    private static string Time(DateTimeOffset? time, string dt)
    {
        return time?.ToString(dt, CultureInfo.InvariantCulture) ?? "";
    }

    private static void WriteCsv<T>(string path, IEnumerable<T> rows, ClassMap<T> map)
    {
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        using var csvWriter = new CsvWriter(sw, CultureInfo.InvariantCulture);

        csvWriter.Context.RegisterClassMap(map);

        csvWriter.WriteHeader<T>();
        csvWriter.NextRecord();
        csvWriter.WriteRecords(rows);
    }

    /// <summary>The columns of 2026.5 in their order; new columns go at the end</summary>
    internal static ClassMap<ActivityOperationEntry> OperationMap(string dt)
    {
        var map = new DefaultClassMap<ActivityOperationEntry>();

        map.Map(t => t.Id).Index(0);
        map.Map(t => t.ActivityTypeOrg).Index(1);
        map.Map(t => t.ActivityType).Index(2);
        map.Map(t => t.Executable).Index(3);
        map.Map(t => t.DisplayText).Index(4);
        map.Map(t => t.ContentInfo).Index(5);
        map.Map(t => t.Payload).Index(6);
        map.Map(t => t.ClipboardPayload).Index(7);
        map.Map(t => t.StartTime).Convert(t => Time(t.Value.StartTime, dt)).Index(8);
        map.Map(t => t.EndTime).Convert(t => Time(t.Value.EndTime, dt)).Index(9);
        map.Map(t => t.Duration).Index(10);
        map.Map(t => t.LastModifiedTime).Convert(t => Time(t.Value.LastModifiedTime, dt)).Index(11);
        map.Map(t => t.LastModifiedTimeOnClient).Convert(t => Time(t.Value.LastModifiedTimeOnClient, dt)).Index(12);
        map.Map(t => t.CreatedTime).Convert(t => Time(t.Value.CreatedTime, dt)).Index(13);
        map.Map(t => t.ExpirationTime).Convert(t => Time(t.Value.ExpirationTime, dt)).Index(14);
        map.Map(t => t.OperationExpirationTime).Convert(t => Time(t.Value.OperationExpirationTime, dt)).Index(15);
        map.Map(t => t.OperationOrder).Index(16);
        map.Map(t => t.AppId).Index(17);
        map.Map(t => t.OperationType).Index(18);
        map.Map(t => t.Description).Index(19);
        map.Map(t => t.PlatformDeviceId).Index(20);
        map.Map(t => t.DevicePlatform).Index(21);
        map.Map(t => t.TimeZone).Index(22);
        map.Map(t => t.AppActivityId).Index(23);
        map.Map(t => t.ClipboardText).Index(24);

        return map;
    }

    internal static ClassMap<ActivityPackageIdEntry> PackageIdMap(string dt)
    {
        var map = new DefaultClassMap<ActivityPackageIdEntry>();

        map.Map(t => t.Id).Index(0);
        map.Map(t => t.Platform).Index(1);
        map.Map(t => t.Name).Index(2);
        map.Map(t => t.AdditionalInformation).Index(3);
        map.Map(t => t.Expires).Convert(t => Time(t.Value.Expires, dt)).Index(4);

        return map;
    }

    internal static ClassMap<ActivityEntry> ActivityMap(string dt)
    {
        var map = new DefaultClassMap<ActivityEntry>();

        map.Map(t => t.Id).Index(0);
        map.Map(t => t.ActivityTypeOrg).Index(1);
        map.Map(t => t.ActivityType).Index(2);
        map.Map(t => t.Executable).Index(3);
        map.Map(t => t.DisplayText).Index(4);
        map.Map(t => t.ContentInfo).Index(5);
        map.Map(t => t.Payload).Index(6);
        map.Map(t => t.ClipboardPayload).Index(7);
        map.Map(t => t.StartTime).Convert(t => Time(t.Value.StartTime, dt)).Index(8);
        map.Map(t => t.EndTime).Convert(t => Time(t.Value.EndTime, dt)).Index(9);
        map.Map(t => t.Duration).Index(10);
        map.Map(t => t.LastModifiedTime).Convert(t => Time(t.Value.LastModifiedTime, dt)).Index(11);
        map.Map(t => t.LastModifiedOnClient).Convert(t => Time(t.Value.LastModifiedOnClient, dt)).Index(12);
        map.Map(t => t.OriginalLastModifiedOnClient).Convert(t => Time(t.Value.OriginalLastModifiedOnClient, dt)).Index(13);
        map.Map(t => t.ExpirationTime).Convert(t => Time(t.Value.ExpirationTime, dt)).Index(14);
        map.Map(t => t.CreatedInCloud).Convert(t => Time(t.Value.CreatedInCloud, dt)).Index(15);
        map.Map(t => t.IsLocalOnly).Index(16);
        map.Map(t => t.ETag).Index(17);
        map.Map(t => t.PackageIdHash).Index(18);
        map.Map(t => t.PlatformDeviceId).Index(19);
        map.Map(t => t.DevicePlatform).Index(20);
        map.Map(t => t.TimeZone).Index(21);
        map.Map(t => t.AppActivityId).Index(22);
        map.Map(t => t.ClipboardText).Index(23);

        return map;
    }

    private class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public CustomHelpAction(HelpAction action)
        {
            _defaultHelp = action;
        }

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);

            Log.Warning("{Msg}", string.Join(" ", parseResult.Tokens));

            return result;
        }
    }
}
