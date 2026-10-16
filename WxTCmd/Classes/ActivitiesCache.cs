using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace WxTCmd.Classes;

/// <summary>What was read from an ActivitiesCache.db</summary>
public class ActivitiesCacheData
{
    public List<ActivityEntry> Activities { get; } = new();
    public List<ActivityOperationEntry> Operations { get; } = new();
    public List<ActivityPackageIdEntry> PackageIds { get; } = new();

    /// <summary>Of Activity, ActivityOperation and Activity_PackageId</summary>
    public List<string> MissingTables { get; } = new();

    /// <summary>Tables that could not be read to the end (a damaged database); the rows before the problem are kept</summary>
    public List<string> TableErrors { get; } = new();

    /// <summary>Values that could not be read; the row is kept with what could be read</summary>
    public List<string> ParseErrors { get; } = new();

    /// <summary>The result of PRAGMA quick_check ("ok" when undamaged)</summary>
    public string QuickCheck { get; set; }

    public bool HasTimelineTables => MissingTables.Count < ActivitiesCache.Tables.Length;
}

/// <summary>Reads the Windows Timeline database (ActivitiesCache.db). Input may be corrupt or hostile</summary>
public static class ActivitiesCache
{
    internal static readonly string[] Tables = { "ActivityOperation", "Activity_PackageId", "Activity" };

    private const int MaxErrorsPerTable = 100;

    private static readonly Regex KnownFolderUri = new(@"^(file:(?:///)?)\{([0-9a-fA-F-]{36})\}(.*)$", RegexOptions.Singleline);

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     Reads the database at <paramref name="path" />. The database (with its -wal and -journal) is copied to a
    ///     temporary folder and only the copy is opened, so nothing is written next to the evidence
    /// </summary>
    /// <exception cref="InvalidDataException">the file is not a SQLite database, or its list of tables cannot be read</exception>
    public static ActivitiesCacheData Read(string path)
    {
        var data = new ActivitiesCacheData();

        using var db = SqliteCopy.Open(path);

        try
        {
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check";
            data.QuickCheck = string.Join("\n", ReadAll(cmd));
        }
        catch (SqliteException e) when (e.SqliteErrorCode == 26)
        {
            throw new InvalidDataException("not a SQLite database", e);
        }
        catch (SqliteException e)
        {
            data.QuickCheck = e.Message;
        }

        HashSet<string> tables;
        try
        {
            //a schema that points past the end of a truncated file is otherwise refused as a whole ("malformed"),
            //losing the rows before the damage. Only the copy is opened, and nothing is written to it
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA writable_schema = ON";
                cmd.ExecuteNonQuery();
            }

            tables = TableNames(db.Connection);
        }
        catch (SqliteException e)
        {
            throw new InvalidDataException(e.SqliteErrorCode == 26 ? "not a SQLite database" : $"its tables cannot be read: {e.Message}", e);
        }

        ReadTable(db.Connection, tables, "ActivityOperation", data, row => data.Operations.Add(ReadOperation(row)));
        ReadTable(db.Connection, tables, "Activity_PackageId", data, row => data.PackageIds.Add(ReadPackageId(row)));
        ReadTable(db.Connection, tables, "Activity", data, row => data.Activities.Add(ReadActivity(row)));

        return data;
    }

    private static List<string> ReadAll(SqliteCommand cmd)
    {
        var list = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture));
        }

        return list;
    }

    private static HashSet<string> TableNames(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        return new HashSet<string>(ReadAll(cmd), StringComparer.OrdinalIgnoreCase);
    }

    private static void ReadTable(SqliteConnection connection, HashSet<string> tables, string table, ActivitiesCacheData data,
        Action<Row> add)
    {
        if (tables.Contains(table) == false)
        {
            data.MissingTables.Add(table);
            return;
        }

        var number = 0;
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM [{table}]";
            using var reader = cmd.ExecuteReader();

            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns[reader.GetName(i)] = i;
            }

            var errors = 0;
            while (reader.Read())
            {
                number++;
                var row = new Row(reader, columns);
                add(row);

                foreach (var error in row.Errors)
                {
                    errors++;
                    if (errors <= MaxErrorsPerTable)
                    {
                        data.ParseErrors.Add($"{table} row {number:N0}: {error}");
                    }
                }
            }

            if (errors > MaxErrorsPerTable)
            {
                data.ParseErrors.Add($"{table}: {errors - MaxErrorsPerTable:N0} more errors");
            }
        }
        catch (SqliteException e)
        {
            data.TableErrors.Add($"{table}: error after {number:N0} rows: {e.Message}");
        }
    }

    private static ActivityEntry ReadActivity(Row row)
    {
        var appId = row.String("AppId");

        var entry = new ActivityEntry
        {
            Id = row.Guid("Id"),
            Executable = row.Try(() => Executable(appId), "AppId", ""),
            ActivityTypeOrg = row.Int("ActivityType"),
            LastModifiedTime = row.Time("LastModifiedTime"),
            ExpirationTime = row.Time("ExpirationTime"),
            CreatedInCloud = row.Time("CreatedInCloud"),
            StartTime = row.Time("StartTime"),
            EndTime = row.Time("EndTime"),
            LastModifiedOnClient = row.Time("LastModifiedOnClient"),
            OriginalLastModifiedOnClient = row.Time("OriginalLastModifiedOnClient"),
            IsLocalOnly = row.Int("IsLocalOnly") == 1,
            ETag = row.Int("ETag"),
            PackageIdHash = row.String("PackageIdHash"),
            PlatformDeviceId = row.String("PlatformDeviceId"),
            AppActivityId = row.String("AppActivityId")
        };

        var payload = ReadPayload(row);
        entry.Payload = payload.Payload;
        entry.DisplayText = payload.DisplayText;
        entry.ContentInfo = payload.ContentInfo;
        entry.DevicePlatform = payload.DevicePlatform;
        entry.TimeZone = payload.TimeZone;

        entry.ClipboardPayload = row.String("ClipboardPayload") ?? "";
        entry.ClipboardText = row.Try(() => ClipboardText(entry.ClipboardPayload), "ClipboardPayload", "");

        return entry;
    }

    private static ActivityOperationEntry ReadOperation(Row row)
    {
        var appId = row.String("AppId");

        var entry = new ActivityOperationEntry
        {
            Id = row.Guid("Id"),
            OperationOrder = row.Int("OperationOrder"),
            OperationType = row.Int("OperationType"),
            AppId = appId,
            Executable = row.Try(() => Executable(appId), "AppId", ""),
            ActivityTypeOrg = row.Int("ActivityType"),
            LastModifiedTime = row.Time("LastModifiedTime"),
            ExpirationTime = row.Time("ExpirationTime"),
            CreatedTime = row.Time("CreatedTime"),
            StartTime = row.Time("StartTime"),
            EndTime = row.Time("EndTime"),
            LastModifiedTimeOnClient = row.Time("LastModifiedOnClient"),
            OperationExpirationTime = row.Time("OperationExpirationTime"),
            PlatformDeviceId = row.String("PlatformDeviceId"),
            AppActivityId = row.String("AppActivityId"),
            Description = ""
        };

        var payload = ReadPayload(row);
        entry.Payload = payload.Payload;
        entry.DisplayText = payload.DisplayText;
        entry.ContentInfo = payload.ContentInfo;
        entry.DevicePlatform = payload.DevicePlatform;
        entry.TimeZone = payload.TimeZone;

        entry.ClipboardPayload = row.String("ClipboardPayload") ?? "";
        entry.ClipboardText = row.Try(() => ClipboardText(entry.ClipboardPayload), "ClipboardPayload", "");

        return entry;
    }

    private static ActivityPackageIdEntry ReadPackageId(Row row)
    {
        var name = row.String("PackageName") ?? "";

        var additional = "";
        if (name.Contains(".exe"))
        {
            additional = MapGuidPrefix(name) ?? "";
        }

        return new ActivityPackageIdEntry(row.Guid("ActivityId"), row.String("Platform"), name, additional, row.Time("ExpirationTime"));
    }

    /// <summary>
    ///     The application from the AppId JSON ([{"application":..., "platform":...}, ...]): the Win32 or executable path
    ///     one, else the UWP one, else the first. A known folder GUID at its start is replaced by the folder's name
    /// </summary>
    internal static string Executable(string appId)
    {
        if (string.IsNullOrEmpty(appId))
        {
            return "";
        }

        using var doc = JsonDocument.Parse(appId);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var apps = doc.RootElement.EnumerateArray()
            .Where(t => t.ValueKind == JsonValueKind.Object)
            .Select(t => (Platform: Property(t, "platform") ?? "", Application: Property(t, "application") ?? ""))
            .ToList();

        if (apps.Count == 0)
        {
            return "";
        }

        var app = apps.FirstOrDefault(t => t.Platform.Equals("windows_win32", StringComparison.OrdinalIgnoreCase) ||
                                           t.Platform.Equals("x_exe_path", StringComparison.OrdinalIgnoreCase));
        if (app.Application == null)
        {
            app = apps.FirstOrDefault(t => t.Platform.Equals("windows_universal", StringComparison.OrdinalIgnoreCase));
        }

        if (app.Application == null)
        {
            app = apps[0];
        }

        return MapGuidPrefix(app.Application) ?? app.Application;
    }

    /// <summary>{known folder GUID}\rest with the GUID replaced by the folder's name, or null when there is none or it is unknown</summary>
    internal static string MapGuidPrefix(string path)
    {
        if (path.StartsWith("{") == false)
        {
            return null;
        }

        var segs = path.Split('\\');
        if (GuidMapping.GuidMapping.TryGetDescription(segs[0], out var name) == false)
        {
            return null;
        }

        segs[0] = name;
        return string.Join("\\", segs);
    }

    private sealed class PayloadInfo
    {
        public string Payload = "";
        public string DisplayText = "";
        public string ContentInfo = "";
        public string DevicePlatform = "";
        public string TimeZone = "";
    }

    private static PayloadInfo ReadPayload(Row row)
    {
        var info = new PayloadInfo();

        var payload = row.String("Payload") ?? "";
        if (payload.StartsWith("{") == false)
        {
            info.Payload = payload.Length == 0 ? "" : "(Binary data)";
            return info;
        }

        info.Payload = payload;

        row.Try(() =>
        {
            using var doc = JsonDocument.Parse(payload);
            var json = doc.RootElement;
            if (json.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            info.TimeZone = Property(json, "userTimezone");
            info.DevicePlatform = Property(json, "devicePlatform");
            info.DisplayText = Property(json, "displayText");

            var description = Property(json, "description");
            var contentUri = Property(json, "contentUri");

            if (contentUri != null || description != null)
            {
                var appName = Property(json, "appDisplayName");
                if (string.IsNullOrEmpty(appName) == false)
                {
                    info.DisplayText = $"{info.DisplayText} ({appName})";
                }

                var uri = ContentUri(contentUri);
                info.ContentInfo = string.IsNullOrEmpty(uri) ? description : $"{description} ({uri})";
            }

            return 0;
        }, "Payload", 0);

        return info;
    }

    /// <summary>URL decoded; a known folder GUID right after file: is replaced by the folder's name</summary>
    internal static string ContentUri(string contentUri)
    {
        if (string.IsNullOrEmpty(contentUri))
        {
            return contentUri;
        }

        var uri = WebUtility.UrlDecode(contentUri);

        var match = KnownFolderUri.Match(uri);
        if (match.Success && GuidMapping.GuidMapping.TryGetDescription(match.Groups[2].Value, out var name))
        {
            return match.Groups[1].Value + name + match.Groups[3].Value;
        }

        return uri;
    }

    /// <summary>
    ///     ClipboardPayload is a JSON array of {"content": base64, "formatName": ...}. The content of the text formats
    ///     (or of entries without a format) that is UTF-8 text
    /// </summary>
    internal static string ClipboardText(string clipboardPayload)
    {
        if (string.IsNullOrWhiteSpace(clipboardPayload) || clipboardPayload.TrimStart().StartsWith("[") == false)
        {
            return "";
        }

        using var doc = JsonDocument.Parse(clipboardPayload);

        var texts = new List<string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var format = Property(item, "formatName");
            var content = Property(item, "content");
            if (string.IsNullOrEmpty(content) || (format != null && format.IndexOf("text", StringComparison.OrdinalIgnoreCase) < 0))
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(content);
            }
            catch (FormatException)
            {
                continue;
            }

            try
            {
                texts.Add(StrictUtf8.GetString(bytes).TrimEnd('\0'));
            }
            catch (DecoderFallbackException)
            {
                //not text
            }
        }

        return string.Join(" | ", texts);
    }

    /// <summary>A property by name, any case; the JSON text when it is not a string</summary>
    private static string Property(JsonElement obj, string name)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Null => null,
                    _ => p.Value.GetRawText()
                };
            }
        }

        return null;
    }

    /// <summary>One row; a missing column reads as null (the schema changed over Windows versions)</summary>
    private sealed class Row
    {
        private readonly Dictionary<string, int> _columns;
        private readonly SqliteDataReader _reader;

        public Row(SqliteDataReader reader, Dictionary<string, int> columns)
        {
            _reader = reader;
            _columns = columns;
        }

        public List<string> Errors { get; } = new();

        private object Value(string column)
        {
            if (_columns.TryGetValue(column, out var i) == false)
            {
                return null;
            }

            try
            {
                return _reader.IsDBNull(i) ? null : _reader.GetValue(i);
            }
            catch (Exception e) when (e is not SqliteException)
            {
                Errors.Add($"{column}: {e.Message}");
                return null;
            }
        }

        public T Try<T>(Func<T> read, string column, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException or ArgumentException)
            {
                Errors.Add($"{column}: {e.Message}");
                return fallback;
            }
        }

        public string String(string column)
        {
            return Value(column) switch
            {
                null => null,
                string s => s,
                byte[] b => Encoding.UTF8.GetString(b),
                var v => Convert.ToString(v, CultureInfo.InvariantCulture)
            };
        }

        public int Int(string column)
        {
            var v = Value(column);
            switch (v)
            {
                case null:
                    return 0;
                case long l when l is >= int.MinValue and <= int.MaxValue:
                    return (int)l;
                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i):
                    return i;
                default:
                    Errors.Add($"{column}: '{Show(v)}' is not a number");
                    return 0;
            }
        }

        /// <summary>Seconds since 1970 (UTC). 0 is no time</summary>
        public DateTimeOffset? Time(string column)
        {
            var v = Value(column);
            long seconds;
            switch (v)
            {
                case null:
                    return null;
                case long l:
                    seconds = l;
                    break;
                case double d when d is >= long.MinValue and <= long.MaxValue:
                    seconds = (long)d;
                    break;
                case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p):
                    seconds = p;
                    break;
                default:
                    Errors.Add($"{column}: '{Show(v)}' is not a time");
                    return null;
            }

            if (seconds == 0)
            {
                return null;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                Errors.Add($"{column}: {seconds} is out of range for a time");
                return null;
            }
        }

        /// <summary>16 bytes as a .NET Guid; anything else as text or hex</summary>
        public string Guid(string column)
        {
            return Value(column) switch
            {
                null => "",
                byte[] { Length: 16 } b => new Guid(b).ToString(),
                byte[] b => BitConverter.ToString(b).Replace("-", ""),
                var v => Convert.ToString(v, CultureInfo.InvariantCulture)
            };
        }

        private static string Show(object v)
        {
            var s = v is byte[] b ? BitConverter.ToString(b, 0, Math.Min(b.Length, 16)) : Convert.ToString(v, CultureInfo.InvariantCulture);
            return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
        }
    }
}
