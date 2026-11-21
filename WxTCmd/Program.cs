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
            Description = "When true, save all console messages to <timestamp>_WxTCmd_<version>_ConsoleLog.txt in the --csv directory. Use --co false to turn it off",
            DefaultValueFactory = _ => true
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
                //an explicit format (the log templates' {Timestamp:HH:mm:ss.fff}) wins over --dt
                return dto.ToString(string.IsNullOrEmpty(format) ? _activeDateTimeFormat : format);
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

        //one UTC time per run, for the CSV names and the console log
        var runTime = DateTimeOffset.UtcNow;

        var consoleLog = co ? ConsoleLog.GetPath(runTime, csv) : null;
        if (consoleLog != null)
        {
            conf.WriteTo.File(consoleLog, outputTemplate: ConsoleLog.Template, formatProvider: formatter);
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

            var ts1 = runTime.ToString("yyyyMMddHHmmss");

            //--dt, for the time stamp columns
            DateTimeFormatConverter.Format = dt;

            if (data.Operations.Count > 0)
            {
                WriteCsv(Path.Combine(csv, $"{ts1}{userProfile}_ActivityOperations.csv"), data.Operations, new ActivityOperationCsvWriter());
            }

            if (data.PackageIds.Count > 0)
            {
                WriteCsv(Path.Combine(csv, $"{ts1}{userProfile}_Activity_PackageIDs.csv"), data.PackageIds);
            }

            if (data.Activities.Count > 0)
            {
                WriteCsv(Path.Combine(csv, $"{ts1}{userProfile}_Activity.csv"), data.Activities, new ActivityCsvWriter());
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

    /// <summary>A CSV with the header and the rows, as CsvHelper 33 wrote them (UTF-8 with a BOM)</summary>
    private static void WriteCsv<T>(string path, IEnumerable<T> rows, CsvRecordWriter<T> recordWriter = null)
    {
        using var csvWriter = CsvWriter.ToFile(path, CsvOutput.Options with { WriteByteOrderMark = true });

        if (recordWriter == null)
        {
            csvWriter.WriteRecords(rows);
        }
        else
        {
            csvWriter.WriteRecords(rows, recordWriter);
        }
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
