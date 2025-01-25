using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.Data;
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
using CsvHelper.TypeConversion;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServiceStack;
using ServiceStack.OrmLite;
using WxTCmd.Classes;
using WxTCmd.Properties;
using DateTimeConverter = ServiceStack.OrmLite.Converters.DateTimeConverter;

namespace WxTCmd;

internal class Program
{

    private static string _activeDateTimeFormat;
    private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    private static RootCommand _rootCommand;

    private static readonly string Header =
        $"WxTCmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
        "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        "\r\nhttps://github.com/EricZimmerman/WxTCmd";

    private static string Footer =
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

    private static async Task Main(string[] args)
    {
        //https://salt4n6.wordpress.com/2018/05/05/windows-10-timeline-forensic-artefacts/amp/?__twitter_impression=true
        //ActivitiesCache.db
        
        _rootCommand = new RootCommand
        {
            new Option<string>(
                "-f",
                "File to process. Required"),

            new Option<string>(
                "--csv",
                "Directory to save CSV formatted results to. Be sure to include the full path in double quotes"),

            new Option<string>(
                "--dt",
                () => "yyyy-MM-dd HH:mm:ss",
                "The custom date/time format to use when displaying timestamps. See https://goo.gl/CNVq0k for options"),
            
            new Option<bool>(
                "--debug",
                () => false,
                "Show debug information during processing"),
            
            new Option<bool>(
                "--trace",
                () => false,
                "Show trace information during processing"),
        };
        
        _rootCommand.Description = Header + "\r\n\r\n" + Footer;

        _rootCommand.Handler = CommandHandler.Create(DoWork);

        await _rootCommand.InvokeAsync(args);
     
        Log.CloseAndFlush();
    }

    class DateTimeOffsetFormatter : IFormatProvider, ICustomFormatter
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
            if (arg is DateTimeOffset)
            {
                var size = (DateTimeOffset)arg;
                return size.ToString(_activeDateTimeFormat);
            }

            var formattable = arg as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(format, _innerFormatProvider);
            }

            return arg.ToString();
        }
    }
    
    private static void DoWork(string f, string csv, string dt, bool debug,bool trace)
    {
        var levelSwitch = new LoggingLevelSwitch();

        _activeDateTimeFormat = dt;
        
        var formatter  =
            new DateTimeOffsetFormatter(CultureInfo.CurrentCulture);

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
            .WriteTo.Console(outputTemplate: template,formatProvider: formatter)
            .MinimumLevel.ControlledBy(levelSwitch);
      
        Log.Logger = conf.CreateLogger();

        if (f.IsNullOrEmpty())
        {
            var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
            var hc = new HelpContext(helpBld,_rootCommand,Console.Out);

            helpBld.Write(hc);

            Log.Warning("-f is required. Exiting");
            Console.WriteLine();
            return;
        }

        if (csv.IsNullOrEmpty())
        {
            var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
            var hc = new HelpContext(helpBld,_rootCommand,Console.Out);

            helpBld.Write(hc);

            Log.Warning("--csv is required. Exiting");
            Console.WriteLine();
            return;
        }

        if (!File.Exists(f))
        {
            var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
            var hc = new HelpContext(helpBld,_rootCommand,Console.Out);

            helpBld.Write(hc);

            Log.Warning("File '{F}' not found. Exiting",f);
            Console.WriteLine();
            return;
        }

        var userProfile = string.Empty;

            
        try {
            userProfile = Regex.Match(f, @"\\Users\\(.+?)\\", RegexOptions.IgnoreCase).Groups[1].Value;

            if (userProfile.Length > 0)
            {
                userProfile = $"_{userProfile}";
            }

        } catch (ArgumentException ) {
            // Syntax error in the regular expression
        }

        Log.Information("{Header}",Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}",string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
        Console.WriteLine();

        if (IsAdministrator() == false)
        {
            Log.Warning("Warning: Administrator privileges not found!");
            Console.WriteLine();
        }
     

        DumpSqliteDll();

        var sw1 = new Stopwatch();
        sw1.Start();

        var apes = new List<ActivityPackageIdEntry>();
        var activitys = new List<ActivityEntry>();
        var aoes = new List<ActivityOperationEntry>();

        var dbFactory = new OrmLiteConnectionFactory(f, SqliteDialect.Provider);

        try
        {
            SqliteDialect.Provider.RegisterConverter<DateTimeOffset>(new EpochConverter());
            SqliteDialect.Provider.RegisterConverter<DateTimeOffset?>(new EpochConverter());

            using (var db = dbFactory.OpenDbConnection())
            {
                try
                {
                    var activityOperations = db.Select<ActivityOperation>();

                    Log.Information("{Table} entries found: {Count:N0}","ActivityOperation",activityOperations.Count);

                    foreach (var op in activityOperations)
                    {
                        string exeName;

                        var appIdInfo = op.AppId.FromJson<List<AppIdInfo>>();

                        var idInfo = appIdInfo.FirstOrDefault(t =>
                            t.Platform.EqualsIgnoreCase("windows_win32") ||
                            t.Platform.EqualsIgnoreCase("x_exe_path"));

                        if (idInfo == null)
                        {
                            idInfo = appIdInfo.First();
                        }

                        if (idInfo.Application.Contains(".exe"))
                        {
                            var segs = idInfo.Application.Split('\\');

                            if (segs[0].StartsWith("{"))
                            {
                                var newname = GuidMapping.GuidMapping.GetDescriptionFromGuid(segs[0]);

                                segs[0] = newname;

                                exeName = string.Join("\\", segs);
                            }
                            else
                            {
                                exeName = idInfo.Application;
                            }
                        }
                        else
                        {
                            exeName = idInfo.Application;
                        }

                        var displayText = string.Empty;
                        var contentInfo = string.Empty;
                        var devicePlatform = string.Empty;
                        var timeZone = string.Empty;
                        var description = string.Empty;

                        var payload = Encoding.ASCII.GetString(op.Payload);

                        var clipPay = string.Empty;

                        if (op.ClipboardPayload is { Length: > 0 })
                        {
                            clipPay = Encoding.ASCII.GetString(op.ClipboardPayload);
                        }

                        if (payload.StartsWith("{"))
                        {
                            var dti = payload.FromJson<PayloadData>();

                            timeZone = dti.UserTimezone;
                            devicePlatform = dti.DevicePlatform;
                            displayText = dti.DisplayText;

                            if (dti.ContentUri != null || dti.Description != null)
                            {
                                displayText = $"{dti.DisplayText} ({dti.AppDisplayName})";

                                var ci = dti.ContentUri.UrlDecode();

                                contentInfo = $"{dti.Description} ({dti.ContentUri.UrlDecode()})";

                                if (ci != null)
                                {
                                    if (ci.Contains("{") & ci.Contains("}"))
                                    {
                                        var start = ci.Substring(0, 5);
                                        var guid = ci.Substring(6, 36);
                                        var end = ci.Substring(43);

                                        var upContent =
                                            $"{start}{GuidMapping.GuidMapping.GetDescriptionFromGuid(guid)}{end}";

                                        contentInfo = $"{dti.Description} ({upContent})";
                                    }
                                }
                            }
                        }
                        else
                        {
                            payload = "(Binary data)";
                        }

                        var aoe = new ActivityOperationEntry(op.Id.ToString(), op.OperationOrder, op.AppId, exeName,
                            op.ActivityType, op.LastModifiedTime, op.ExpirationTime, payload, op.CreatedTime,
                            op.EndTime, op.LastModifiedOnClient, op.OperationExpirationTime, op.PlatformDeviceId,
                            op.OperationType, devicePlatform, timeZone, description, op.StartTime, displayText,
                            clipPay, contentInfo);

                        aoes.Add(aoe);
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("no such table"))
                    {
                        Log.Error("{Table} table does not exist!","ActivityOperation");
                    }
                    else
                    {
                        Log.Error(e,"Error processing {Table} table: {Message}","ActivityOperation",e.Message);
                    }
                }

                try
                {
                    var activityPackageIds = db.Select<ActivityPackageId>();

                    Log.Information("{Table} entries found: {Count:N0}","Activity_PackageId",activityPackageIds.Count);

                    foreach (var packageId in activityPackageIds)
                    {
                        var exeName = string.Empty;

                        if (packageId.PackageName.Contains(".exe"))
                        {
                            var segs = packageId.PackageName.Split('\\');

                            if (segs[0].StartsWith("{"))
                            {
                                var newname = GuidMapping.GuidMapping.GetDescriptionFromGuid(segs[0]);

                                segs[0] = newname;

                                exeName = string.Join("\\", segs);
                            }
                        }

                        var ape = new ActivityPackageIdEntry(packageId.ActivityId.ToString(), packageId.Platform,
                            packageId.PackageName, exeName, packageId.ExpirationTime);

                        apes.Add(ape);
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("no such table"))
                    {
                        Log.Error("{Table} table does not exist!","ActivityPackageId");
                    }
                    else
                    {
                        Log.Error(e,"Error processing {Table} table: {Message}","ActivityPackageId",e.Message);
                    }
                }

                try
                {
                    var activities = db.Select<Classes.Activity>();

                    Log.Information("{Table} entries found: {Count:N0}","Activity",activities.Count);

                    foreach (var act in activities)
                    {
                        var foo = act.AppId.FromJson<List<AppIdInfo>>();

                        var win32 = foo.FirstOrDefault(
                            t => t.Platform == "windows_win32" || t.Platform == "x_exe_path");

                        string exe;

                        if (win32 != null)
                        {
                            exe = win32.Application;
                        }
                        else
                        {
                            var wu = foo.FirstOrDefault(t => t.Platform == "windows_universal");
                            if (wu != null)
                            {
                                exe = wu.Application;
                            }
                            else
                            {
                                exe = foo.First().Application;
                            }
                        }

                        if (exe.StartsWith("{"))
                        {
                            var segs = exe.Split('\\');

                            if (segs[0].StartsWith("{"))
                            {
                                var newname = GuidMapping.GuidMapping.GetDescriptionFromGuid(segs[0]);

                                segs[0] = newname;

                                exe = string.Join("\\", segs);
                            }
                        }

                        var displayText = string.Empty;
                        var contentInfo = string.Empty;
                        var devicePlatform = string.Empty;
                        var timeZone = string.Empty;

                        var clipPay = string.Empty;

                        if (act.ClipboardPayload is { Length: > 0 })
                        {
                            clipPay = Encoding.ASCII.GetString(act.ClipboardPayload);
                        }

                        var payload = Encoding.ASCII.GetString(act.Payload);

                        if (payload.StartsWith("{"))
                        {
                            var dti = payload.FromJson<PayloadData>();

                            timeZone = dti.UserTimezone;
                            devicePlatform = dti.DevicePlatform;
                            displayText = dti.DisplayText;

                            if (dti.ContentUri != null || dti.Description != null)
                            {
                                displayText = $"{dti.DisplayText} ({dti.AppDisplayName})";

                                var ci = dti.ContentUri.UrlDecode();

                                contentInfo = $"{dti.Description} ({dti.ContentUri.UrlDecode()})";

                                if (ci != null)
                                {
                                    if (ci.Contains("{") & ci.Contains("}"))
                                    {
                                        var start = ci.Substring(0, 5);
                                        var guid = ci.Substring(6, 36);
                                        var end = ci.Substring(43);

                                        var upContent =
                                            $"{start}{GuidMapping.GuidMapping.GetDescriptionFromGuid(guid)}{end}";

                                        contentInfo = $"{dti.Description} ({upContent})";
                                    }
                                }
                            }
                        }
                        else
                        {
                            payload = "(Binary data)";
                        }

                        var a = new ActivityEntry(act.Id.ToString(), exe, displayText, contentInfo,
                            act.LastModifiedTime, act.ExpirationTime, act.CreatedInCloud, act.StartTime,
                            act.EndTime,
                            act.LastModifiedOnClient, act.OriginalLastModifiedOnClient, act.ActivityType,
                            act.IsLocalOnly == 1, act.ETag, act.PackageIdHash, act.PlatformDeviceId, devicePlatform,
                            timeZone, payload,   clipPay);

                        activitys.Add(a);
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("no such table"))
                    {
                        Log.Error("{Activity} table does not exist!","Activity");
                    }
                    else
                    {
                        Log.Error(e,"Error processing {Activity} table: {Message}","Activity",e.Message);
                    }
                }
            }

            //write out csv files

            if (Directory.Exists(csv) == false)
            {
                try
                {
                    Directory.CreateDirectory(csv);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "There was an error creating directory '{Csv}'. Error: {Message} Exiting",csv);
                    return;
                }
            }

            var ts1 = DateTime.Now.ToString("yyyyMMddHHmmss");

            if (aoes.Count > 0)
            {
                var aoesFile = $"{ts1}{userProfile}_ActivityOperations.csv";
                var aoesOut = Path.Combine(csv, aoesFile);

                using var sw = new StreamWriter(aoesOut, false, Encoding.UTF8);
                var csvWriter = new CsvWriter(sw, CultureInfo.InvariantCulture);

                var o = new TypeConverterOptions
                {
                    DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                };
                csvWriter.Context.TypeConverterOptionsCache.AddOptions<ActivityOperationEntry>(o);

                var foo = csvWriter.Context.AutoMap<ActivityOperationEntry>();

                foo.Map(t => t.Id).Index(0);
                foo.Map(t => t.ActivityTypeOrg).Index(1);
                foo.Map(t => t.ActivityType).Index(2);
                foo.Map(t => t.Executable).Index(3);
                foo.Map(t => t.DisplayText).Index(4);
                foo.Map(t => t.ContentInfo).Index(5);
                foo.Map(t => t.Payload).Index(6);
                foo.Map(t => t.ClipboardPayload).Index(7);
                foo.Map(t => t.StartTime).Convert(t =>
                    t.Value.StartTime.Year == 1
                        ? ""
                        : t.Value.StartTime.ToString(dt)).Index(8);
                foo.Map(t => t.EndTime).Convert(t =>
                    t.Value.EndTime?.Year == 1
                        ? ""
                        : t.Value.EndTime?.ToString(dt)).Index(9);
                foo.Map(t => t.Duration).Index(10);
                foo.Map(t => t.LastModifiedTime).Convert(t =>
                    t.Value.LastModifiedTime.ToString(dt)).Index(11);
                foo.Map(t => t.LastModifiedTimeOnClient).Convert(t =>
                        t.Value.LastModifiedTimeOnClient.ToString(dt))
                    .Index(12);
                foo.Map(t => t.CreatedTime).Convert(t =>
                    t.Value.CreatedTime.ToString(dt)).Index(13);

                foo.Map(t => t.ExpirationTime).Convert(t =>
                    t.Value.ExpirationTime.ToString(dt)).Index(14);
                foo.Map(t => t.OperationExpirationTime).Convert(t =>
                        t.Value.OperationExpirationTime.ToString(dt))
                    .Index(15);

                foo.Map(t => t.OperationOrder).Index(16);

                foo.Map(t => t.AppId).Index(17);

                foo.Map(t => t.OperationType).Index(18);
                foo.Map(t => t.Description).Index(19);

                foo.Map(t => t.PlatformDeviceId).Index(20);
                foo.Map(t => t.DevicePlatform).Index(21);
                foo.Map(t => t.TimeZone).Index(22);


                csvWriter.Context.RegisterClassMap(foo);

                csvWriter.WriteHeader<ActivityOperationEntry>();
                csvWriter.NextRecord();
                csvWriter.WriteRecords(aoes);

                sw.Flush();
            }

            if (apes.Count > 0)
            {
                var apesFile = $"{ts1}{userProfile}_Activity_PackageIDs.csv";
                var apesOut = Path.Combine(csv, apesFile);

                using var sw = new StreamWriter(apesOut, false, Encoding.UTF8);
                var csvWriter = new CsvWriter(sw, CultureInfo.InvariantCulture);

                var o = new TypeConverterOptions
                {
                    DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                };
                csvWriter.Context.TypeConverterOptionsCache.AddOptions<ActivityPackageIdEntry>(o);

                var foo = csvWriter.Context.AutoMap<ActivityPackageIdEntry>();

                foo.Map(t => t.Id).Index(0);
                foo.Map(t => t.Platform).Index(1);
                foo.Map(t => t.Name).Index(2);
                foo.Map(t => t.AdditionalInformation).Index(3);
                foo.Map(t => t.Expires)
                    .Convert(t => t.Value.Expires.ToString(dt))
                    .Index(4);

                csvWriter.Context.RegisterClassMap(foo);

                csvWriter.WriteHeader<ActivityPackageIdEntry>();
                csvWriter.NextRecord();
                csvWriter.WriteRecords(apes);

                sw.Flush();
            }

            if (activitys.Count > 0)
            {
                var actsFile = $"{ts1}{userProfile}_Activity.csv";
                var actsOut = Path.Combine(csv, actsFile);

                using var sw = new StreamWriter(actsOut, false, Encoding.UTF8);
                var csvWriter = new CsvWriter(sw, CultureInfo.InvariantCulture);

                var o = new TypeConverterOptions
                {
                    DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                };
                csvWriter.Context.TypeConverterOptionsCache.AddOptions<ActivityEntry>(o);

                var foo = csvWriter.Context.AutoMap<ActivityEntry>();

                foo.Map(t => t.Id).Index(0);
                foo.Map(t => t.ActivityTypeOrg).Index(1);
                foo.Map(t => t.ActivityType).Index(2);
                foo.Map(t => t.Executable).Index(3);
                foo.Map(t => t.DisplayText).Index(4);
                foo.Map(t => t.ContentInfo).Index(5);
                foo.Map(t => t.Payload).Index(6);
                foo.Map(t => t.ClipboardPayload).Index(7);
                foo.Map(t => t.StartTime)
                    .Convert(t => t.Value.StartTime.ToString(dt))
                    .Index(8);
                foo.Map(t => t.EndTime).Convert(t =>
                    t.Value.EndTime?.ToString(dt) + "").Index(9);
                foo.Map(t => t.Duration).Index(10);
                foo.Map(t => t.LastModifiedTime).Convert(t =>
                    t.Value.LastModifiedTime.ToString(dt)).Index(11);
                foo.Map(t => t.LastModifiedOnClient).Convert(t =>
                    t.Value.LastModifiedOnClient.ToString(dt)).Index(12);
                foo.Map(t => t.OriginalLastModifiedOnClient).Convert(t =>
                        t.Value.OriginalLastModifiedOnClient?.ToString(dt) +
                        "")
                    .Index(13);
                foo.Map(t => t.ExpirationTime).Convert(t =>
                    t.Value.ExpirationTime.ToString(dt)).Index(14);
                foo.Map(t => t.CreatedInCloud).Convert(t =>
                    t.Value.CreatedInCloud?.ToString(dt) + "").Index(15);

                foo.Map(t => t.IsLocalOnly).Index(16);
                foo.Map(t => t.ETag).Index(17);
                foo.Map(t => t.PackageIdHash).Index(18);

                foo.Map(t => t.PlatformDeviceId).Index(19);
                foo.Map(t => t.DevicePlatform).Index(20);
                foo.Map(t => t.TimeZone).Index(21);

                csvWriter.Context.RegisterClassMap(foo);

                csvWriter.WriteHeader<ActivityEntry>();
                csvWriter.NextRecord();
                csvWriter.WriteRecords(activitys);

                sw.Flush();
            }
        }
        catch (Exception e)
        {
            if (e.Message.Contains("file is not a database"))
            {
                Log.Error(
                    "Error processing database: '{F}' is not a sqlite database",f);
            }
            else
            {
                Log.Error(e,"Error processing database: {Message}",e.Message);
            }
        }
        

        sw1.Stop();

        Console.WriteLine();
        Log.Information("Results saved to: {Csv}",csv);

        Console.WriteLine();
        Log.Information(
            "Processing complete in {TotalSeconds:N4} seconds",sw1.Elapsed.TotalSeconds);
        Console.WriteLine();

#if NET6_0_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)){ 
        if (File.Exists("libSQLite.Interop.so"))
        {
            try
            {
                File.Delete("libSQLite.Interop.so");
            }
            catch (Exception)
            {
                Log.Warning("Unable to delete {Sql}. Delete manually if needed","libSQLite.Interop.so");
                Console.WriteLine();
            }
        }
        } else {
        if (File.Exists("SQLite.Interop.dll"))
        {
            try
            {
                File.Delete("SQLite.Interop.dll");
            }
            catch (Exception)
            {
                Log.Warning("Unable to delete {Sql}. Delete manually if needed","SQLite.Interop.dll");
                Console.WriteLine();
            }
        }
        }
#else
        if (File.Exists("SQLite.Interop.dll"))
        {
            try
            {
                File.Delete("SQLite.Interop.dll");
            }
            catch (Exception)
            {
                Log.Warning("Unable to delete {Sql}. Delete manually if needed","SQLite.Interop.dll");
                Console.WriteLine();
            }
        }
#endif        
    }

    private static void DumpSqliteDll()
    {
#if NET6_0_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)){
        var sqllitefile = "libSQLite.Interop.so"; 

        if (Environment.Is64BitProcess)
        {
            File.WriteAllBytes(sqllitefile, Resources.x64SQLite_Interop_linux);
        }
        else
        {
            //32 Bit Not Tested on Linux
            //File.WriteAllBytes(sqllitefile, Resources.x86SQLite_Interop_linux);
            Log.Warning("32 Bit Linux Not Supported! Exiting");
            Console.WriteLine();
            Environment.Exit(-1);
        }
        } else {

        var sqllitefile = "SQLite.Interop.dll";

        if (Environment.Is64BitProcess)
        {
            File.WriteAllBytes(sqllitefile, Resources.x64SQLite_Interop);
        }
        else
        {
            File.WriteAllBytes(sqllitefile, Resources.x86SQLite_Interop);
        }
        }
#else
        var sqllitefile = "SQLite.Interop.dll";

        if (Environment.Is64BitProcess)
        {
            File.WriteAllBytes(sqllitefile, Resources.x64SQLite_Interop);
        }
        else
        {
            File.WriteAllBytes(sqllitefile, Resources.x86SQLite_Interop);
        }
#endif        
    }

  
}

public class EpochConverter : DateTimeConverter
{
    public override string ColumnDefinition => "DATETIME";

    public override DbType DbType => DbType.DateTime;

    public override object FromDbValue(Type fieldType, object value)
    {
        return (DateTimeOffset) value;
    }

    public override object GetValue(IDataReader reader, int columnIndex, object[] values)
    {
        if (reader.IsDBNull(columnIndex))
        {
            return null;
        }

        var val = 0;
        try
        {
            val = reader.GetInt32(columnIndex);
        }
        catch (Exception)
        {
            // ignored
        }

        if (val == 0)
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(val).ToUniversalTime();
    }
}


internal class ApplicationArguments
{
    public string File { get; set; }

    public string CsvDirectory { get; set; }

    public string DateTimeFormat { get; set; }

    public bool Debug { get; set; }
}