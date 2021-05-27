using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.TypeConversion;
using Fclp;
using Fclp.Internals.Extensions;
using NLog;
using NLog.Config;
using NLog.Targets;
using ServiceStack;
using ServiceStack.OrmLite;
using WxTCmd.Classes;
using WxTCmd.Properties;
using DateTimeConverter = ServiceStack.OrmLite.Converters.DateTimeConverter;

namespace WxTCmd
{
    internal class Program
    {
        private static Logger _logger;

        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);


        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void Main(string[] args)
        {
            //https://salt4n6.wordpress.com/2018/05/05/windows-10-timeline-forensic-artefacts/amp/?__twitter_impression=true

            SetupNLog();

            _logger = LogManager.GetLogger("Main");

            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>
            {
                IsCaseSensitive = false
            };

            _fluentCommandLineParser.Setup(arg => arg.File)
                .As('f')
                .WithDescription("File to process. Required");

            _fluentCommandLineParser.Setup(arg => arg.CsvDirectory)
                .As("csv")
                .WithDescription(
                    "Directory to save CSV formatted results to. Be sure to include the full path in double quotes");

//            _fluentCommandLineParser.Setup(arg => arg.Debug)
//                .As("Debug")
//                .WithDescription("Debug mode\r\n")
//                .SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying timestamps. See https://goo.gl/CNVq0k for options. Default is: yyyy-MM-dd HH:mm:ss")
                .SetDefault("yyyy-MM-dd HH:mm:ss");

            var header =
                $"WxTCmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/WxTCmd";

            var footer =
                @"Examples: WxTCmd.exe -f ""C:\Users\eric\AppData\Local\ConnectedDevicesPlatform\L.eric\ActivitiesCache.db"" --csv c:\temp" +
                "\r\n\t " +
                "\r\n\t" +
                @"  Database files are typically found at 'C:\Users\<profile>\AppData\Local\ConnectedDevicesPlatform\L.<profile>\ActivitiesCache.db'" +
                "\r\n\t" +
                "\r\n\t" +
                "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

            _fluentCommandLineParser.SetupHelp("?", "help")
                .WithHeader(header)
                .Callback(text => _logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _logger.Error("");
                _logger.Error(result.ErrorText);

                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                return;
            }

            if (_fluentCommandLineParser.Object.File.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("-f is required. Exiting");
                return;
            }

            if (_fluentCommandLineParser.Object.CsvDirectory.IsNullOrEmpty())
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("--csv is required. Exiting");
                return;
            }

            if (!File.Exists(_fluentCommandLineParser.Object.File))
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn($"File '{_fluentCommandLineParser.Object.File}' not found. Exiting");
                return;
            }

            var _userProfile = string.Empty;

            
            try {
                _userProfile = Regex.Match(_fluentCommandLineParser.Object.File, @"\\Users\\(.+?)\\", RegexOptions.IgnoreCase).Groups[1].Value;

                if (_userProfile.Length > 0)
                {
                    _userProfile = $"_{_userProfile}";
                }

            } catch (ArgumentException ) {
                // Syntax error in the regular expression
            }
            

            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

            if (IsAdministrator() == false)
            {
                _logger.Fatal("Warning: Administrator privileges not found!\r\n");
            }

            if (_fluentCommandLineParser.Object.Debug)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Debug);
                LogManager.ReconfigExistingLoggers();
            }

            DumpSqliteDll();

            var sw1 = new Stopwatch();
            sw1.Start();

            var apes = new List<ActivityPackageIdEntry>();
            var activitys = new List<ActivityEntry>();
            var aoes = new List<ActivityOperationEntry>();

            var dbFactory = new OrmLiteConnectionFactory(
                _fluentCommandLineParser.Object.File,
                SqliteDialect.Provider);

            try
            {
                SqliteDialect.Provider.RegisterConverter<DateTimeOffset>(new EpochConverter());
                SqliteDialect.Provider.RegisterConverter<DateTimeOffset?>(new EpochConverter());

                using (var db = dbFactory.OpenDbConnection())
                {
                    try
                    {
                        var activityOperations = db.Select<ActivityOperation>();

                        _logger.Info($"ActivityOperation entries found: {activityOperations.Count:N0}");

                        foreach (var op in activityOperations)
                        {
                            var exeName = string.Empty;

                            var AppIdInfo = op.AppId.FromJson<List<AppIdInfo>>();

                            var idInfo = AppIdInfo.FirstOrDefault(t =>
                                t.Platform.EqualsIgnoreCase("windows_win32") ||
                                t.Platform.EqualsIgnoreCase("x_exe_path"));

                            if (idInfo == null)
                            {
                                idInfo = AppIdInfo.First();
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

                            if (op.ClipboardPayload.IsNullOrEmpty() == false)
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
                            _logger.Error("ActivityOperation table does not exist!");
                        }
                        else
                        {
                            _logger.Error($"Error processing ActivityOperation table: {e.Message}");
                        }
                    }


                    try
                    {
                        var activityPackageIds = db.Select<ActivityPackageId>();

                        _logger.Info($"Activity_PackageId entries found: {activityPackageIds.Count:N0}");

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
                            _logger.Error("ActivityPackageId table does not exist!");
                        }
                        else
                        {
                            _logger.Error($"Error processing ActivityPackageId table: {e.Message}");
                        }
                    }

                    try
                    {
                        var activities = db.Select<Activity>();

                        _logger.Info($"Activity entries found: {activities.Count:N0}");

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

                            if (act.ClipboardPayload.IsNullOrEmpty() == false)
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
                            _logger.Error("Activity table does not exist!");
                        }
                        else
                        {
                            _logger.Error($"Error processing Activity table: {e.Message}");
                        }
                    }
                }

                //write out csvs

                if (Directory.Exists(_fluentCommandLineParser.Object.CsvDirectory) == false)
                {
                    try
                    {
                        Directory.CreateDirectory(_fluentCommandLineParser.Object.CsvDirectory);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            $"There was an error creating directory '{_fluentCommandLineParser.Object.CsvDirectory}'. Error: {ex.Message} Exiting");
                        return;
                    }
                }

                var ts1 = DateTime.Now.ToString("yyyyMMddHHmmss");

                if (aoes.Count > 0)
                {
                    var aoesFile = $"{ts1}{_userProfile}_ActivityOperations.csv";
                    var aoesOut = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, aoesFile);

                    using (var sw = new StreamWriter(aoesOut, false, Encoding.UTF8))
                    {
                        var csv = new CsvWriter(sw, CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csv.Context.TypeConverterOptionsCache.AddOptions<ActivityOperationEntry>(o);


                        var foo = csv.Context.AutoMap<ActivityOperationEntry>();

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
                                : t.Value.StartTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(8);
                        foo.Map(t => t.EndTime).Convert(t =>
                            t.Value.EndTime?.Year == 1
                                ? ""
                                : t.Value.EndTime?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(9);
                        foo.Map(t => t.Duration).Index(10);
                        foo.Map(t => t.LastModifiedTime).Convert(t =>
                            t.Value.LastModifiedTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(11);
                        foo.Map(t => t.LastModifiedTimeOnClient).Convert(t =>
                                t.Value.LastModifiedTimeOnClient.ToString(_fluentCommandLineParser.Object.DateTimeFormat))
                            .Index(12);
                        foo.Map(t => t.CreatedTime).Convert(t =>
                            t.Value.CreatedTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(13);

                        foo.Map(t => t.ExpirationTime).Convert(t =>
                            t.Value.ExpirationTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(14);
                        foo.Map(t => t.OperationExpirationTime).Convert(t =>
                                t.Value.OperationExpirationTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat))
                            .Index(15);

                        foo.Map(t => t.OperationOrder).Index(16);

                        foo.Map(t => t.AppId).Index(17);

                        foo.Map(t => t.OperationType).Index(18);
                        foo.Map(t => t.Description).Index(19);

                        foo.Map(t => t.PlatformDeviceId).Index(20);
                        foo.Map(t => t.DevicePlatform).Index(21);
                        foo.Map(t => t.TimeZone).Index(22);


                        csv.Context.RegisterClassMap(foo);

                        csv.WriteHeader<ActivityOperationEntry>();
                        csv.NextRecord();
                        csv.WriteRecords(aoes);

                        sw.Flush();
                    }
                }

                if (apes.Count > 0)
                {
                    var apesFile = $"{ts1}{_userProfile}_Activity_PackageIDs.csv";
                    var apesOut = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, apesFile);

                    using (var sw = new StreamWriter(apesOut, false, Encoding.UTF8))
                    {
                        var csv = new CsvWriter(sw, CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csv.Context.TypeConverterOptionsCache.AddOptions<ActivityPackageIdEntry>(o);

                        var foo = csv.Context.AutoMap<ActivityPackageIdEntry>();

                        foo.Map(t => t.Id).Index(0);
                        foo.Map(t => t.Platform).Index(1);
                        foo.Map(t => t.Name).Index(2);
                        foo.Map(t => t.AdditionalInformation).Index(3);
                        foo.Map(t => t.Expires)
                            .Convert(t => t.Value.Expires.ToString(_fluentCommandLineParser.Object.DateTimeFormat))
                            .Index(4);

                        csv.Context.RegisterClassMap(foo);

                        csv.WriteHeader<ActivityPackageIdEntry>();
                        csv.NextRecord();
                        csv.WriteRecords(apes);

                        sw.Flush();
                    }
                }

                if (activitys.Count > 0)
                {
                    var actsFile = $"{ts1}{_userProfile}_Activity.csv";
                    var actsOut = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, actsFile);

                    using (var sw = new StreamWriter(actsOut, false, Encoding.UTF8))
                    {
                        var csv = new CsvWriter(sw, CultureInfo.InvariantCulture);

                        var o = new TypeConverterOptions
                        {
                            DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                        };
                        csv.Context.TypeConverterOptionsCache.AddOptions<ActivityEntry>(o);

                        var foo = csv.Context.AutoMap<ActivityEntry>();

                        foo.Map(t => t.Id).Index(0);
                        foo.Map(t => t.ActivityTypeOrg).Index(1);
                        foo.Map(t => t.ActivityType).Index(2);
                        foo.Map(t => t.Executable).Index(3);
                        foo.Map(t => t.DisplayText).Index(4);
                        foo.Map(t => t.ContentInfo).Index(5);
                        foo.Map(t => t.Payload).Index(6);
                        foo.Map(t => t.ClipboardPayload).Index(7);
                        foo.Map(t => t.StartTime)
                            .Convert(t => t.Value.StartTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat))
                            .Index(8);
                        foo.Map(t => t.EndTime).Convert(t =>
                            t.Value.EndTime?.ToString(_fluentCommandLineParser.Object.DateTimeFormat) + "").Index(9);
                        foo.Map(t => t.Duration).Index(10);
                        foo.Map(t => t.LastModifiedTime).Convert(t =>
                            t.Value.LastModifiedTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(11);
                        foo.Map(t => t.LastModifiedOnClient).Convert(t =>
                            t.Value.LastModifiedOnClient.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(12);
                        foo.Map(t => t.OriginalLastModifiedOnClient).Convert(t =>
                                t.Value.OriginalLastModifiedOnClient?.ToString(_fluentCommandLineParser.Object
                                    .DateTimeFormat) +
                                "")
                            .Index(13);
                        foo.Map(t => t.ExpirationTime).Convert(t =>
                            t.Value.ExpirationTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)).Index(14);
                        foo.Map(t => t.CreatedInCloud).Convert(t =>
                            t.Value.CreatedInCloud?.ToString(_fluentCommandLineParser.Object.DateTimeFormat) + "").Index(15);

                        foo.Map(t => t.IsLocalOnly).Index(16);
                        foo.Map(t => t.ETag).Index(17);
                        foo.Map(t => t.PackageIdHash).Index(18);

                        foo.Map(t => t.PlatformDeviceId).Index(19);
                        foo.Map(t => t.DevicePlatform).Index(20);
                        foo.Map(t => t.TimeZone).Index(21);

                        csv.Context.RegisterClassMap(foo);

                        csv.WriteHeader<ActivityEntry>();
                        csv.NextRecord();
                        csv.WriteRecords(activitys);

                        sw.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                if (e.Message.Contains("file is not a database"))
                {
                    _logger.Error(
                        $"Error processing database: '{_fluentCommandLineParser.Object.File}' is not a sqlite database");
                }
                else
                {
                    _logger.Error($"Error processing database: {e.Message}");
                }
            }
            finally
            {
                dbFactory = null;
            }


            sw1.Stop();

            _logger.Info($"\r\nResults saved to: {_fluentCommandLineParser.Object.CsvDirectory}");


            _logger.Info(
                $"\r\nProcessing complete in {sw1.Elapsed.TotalSeconds:N4} seconds\r\n");


            if (File.Exists("SQLite.Interop.dll"))
            {
                try
                {
                    File.Delete("SQLite.Interop.dll");
                }
                catch (Exception)
                {
                    _logger.Warn("Unable to delete 'SQLite.Interop.dll'. Delete manually if needed.\r\n");
                }
            }
        }

        private static void DumpSqliteDll()
        {
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

        private static void SetupNLog()
        {
            if (File.Exists(Path.Combine(BaseDirectory, "Nlog.config")))
            {
                return;
            }

            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }
    }

    public class EpochConverter : DateTimeConverter
    {
        public override string ColumnDefinition => "DATETIME";

        public override DbType DbType => DbType.DateTime;

        public override string ToQuotedString(Type fieldType, object value)
        {
            return base.ToQuotedString(fieldType, value);
        }

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
}