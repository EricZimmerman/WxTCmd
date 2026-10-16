using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace WxTCmd.Test;

/// <summary>A folder of its own per test, with the samples and databases made with the real schemas</summary>
public abstract class TestData
{
    protected string Dir;

    [SetUp]
    public void SetUp()
    {
        Dir = Path.Combine(Path.GetTempPath(), "wxtcmd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(Dir, true);
    }

    /// <summary>
    ///     A sample in TestFiles (DFIRArtifactMuseum/Win11/RathbunVM, DFIRArtifactMuseum/Win10/BelkasoftCTF_InsiderThreat_L.User,
    ///     plaso), decompressed into Users/user/folder (so the CSV names get _user)
    /// </summary>
    protected string Sample(string name, string folder = "L.user")
    {
        var source = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", name);
        var target = Path.Combine(Dir, "Users", "user", folder);
        Directory.CreateDirectory(target);

        foreach (var gzFile in Directory.GetFiles(source, "*.gz"))
        {
            using var gz = new GZipStream(File.OpenRead(gzFile), CompressionMode.Decompress);
            using var output = File.Create(Path.Combine(target, Path.GetFileNameWithoutExtension(gzFile)));
            gz.CopyTo(output);
        }

        return Path.Combine(target, "ActivitiesCache.db");
    }

    /// <summary>Name and SHA-256 of every file in the folder</summary>
    protected static Dictionary<string, string> Hashes(string folder)
    {
        return Directory.GetFiles(folder).ToDictionary(Path.GetFileName, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
    }

    /// <summary>The tables as Windows 11 creates them (user_version 29, from the Museum's Win11 sample)</summary>
    public const string Schema =
        "CREATE TABLE [Activity]([Id] GUID PRIMARY KEY NOT NULL, [AppId] TEXT NOT NULL, [PackageIdHash] TEXT, [AppActivityId] TEXT, [ActivityType] INT NOT NULL, [ActivityStatus] INT NOT NULL, [ParentActivityId] GUID, [Tag] TEXT, [Group] TEXT, [MatchId] TEXT, [LastModifiedTime] DATETIME NOT NULL, [ExpirationTime] DATETIME, [Payload] BLOB, [Priority] INT, [IsLocalOnly] INT, [PlatformDeviceId] TEXT, [DdsDeviceId] TEXT, [CreatedInCloud] DATETIME, [StartTime] DATETIME, [EndTime] DATETIME, [LastModifiedOnClient] DATETIME, [GroupAppActivityId] TEXT, [ClipboardPayload] BLOB, [EnterpriseId] TEXT, [OriginalPayload] BLOB, [UserActionState] INT,[IsRead] INT,[OriginalLastModifiedOnClient] DATETIME, [GroupItems] TEXT, [LocalExpirationTime] DATETIME, [ETag] INT NOT NULL);" +
        "CREATE TABLE [ActivityOperation]([OperationOrder] INTEGER PRIMARY KEY ASC NOT NULL, [Id] GUID NOT NULL, [OperationType] INT NOT NULL, [AppId] TEXT NOT NULL, [PackageIdHash] TEXT, [AppActivityId] TEXT, [ActivityType] INT NOT NULL, [ParentActivityId] GUID, [Tag] TEXT, [Group] TEXT, [MatchId] TEXT, [LastModifiedTime] DATETIME NOT NULL, [ExpirationTime] DATETIME, [Payload] BLOB, [Priority] INT, [CreatedTime] DATETIME, [OperationExpirationTime] DATETIME,[PlatformDeviceId] TEXT, [DdsDeviceId] TEXT, [CreatedInCloud] DATETIME, [StartTime] DATETIME NOT NULL, [EndTime] DATETIME, [LastModifiedOnClient] DATETIME NOT NULL, [CorrelationVector] TEXT, [GroupAppActivityId] TEXT, [ClipboardPayload] BLOB, [EnterpriseId] TEXT, [UserActionState] INT,[IsRead] INT,[OriginalPayload] BLOB, [OriginalLastModifiedOnClient] DATETIME, [UploadAllowedByPolicy] INT NOT NULL DEFAULT 1, [PatchFields] BLOB, [GroupItems] TEXT, [ThrottleReleaseTime] DATETIME, [ETag] INT NOT NULL);" +
        "CREATE TABLE [Activity_PackageId]([ActivityId] GUID NOT NULL, [Platform] TEXT NOT NULL COLLATE NOCASE, [PackageName] TEXT NOT NULL COLLATE NOCASE, [ExpirationTime] DATETIME NOT NULL);";

    public const string AppId = "[{\"application\":\"{6D809377-6AF0-444B-8957-A3773F02200E}\\\\Notepad++\\\\notepad++.exe\",\"platform\":\"windows_win32\"},{\"application\":\"\",\"platform\":\"packageId\"}]";

    /// <summary>A row with every NOT NULL column set; <paramref name="values" /> overrides or adds columns</summary>
    public static Dictionary<string, object> ActivityRow(Guid id, params (string Column, object Value)[] values)
    {
        var row = new Dictionary<string, object>
        {
            ["Id"] = id.ToByteArray(), ["AppId"] = AppId, ["ActivityType"] = 5, ["ActivityStatus"] = 1, ["LastModifiedTime"] = 1646000000L,
            ["StartTime"] = 1646000000L, ["EndTime"] = 0L, ["ExpirationTime"] = 1648592000L, ["ETag"] = 1, ["AppActivityId"] = "ECB32AF3-1440-4086-94E3-5311F97F89C4",
            ["Payload"] = "{\"displayText\":\"notepad++.exe\",\"activationUri\":\"ms-shellactivity:\",\"appDisplayName\":\"Notepad++\",\"backgroundColor\":\"black\"}"u8.ToArray()
        };

        foreach (var (column, value) in values)
        {
            row[column] = value;
        }

        return row;
    }

    public static Dictionary<string, object> OperationRow(int order, params (string Column, object Value)[] values)
    {
        var row = ActivityRow(Guid.NewGuid(), values);
        row.Remove("ActivityStatus");
        row["OperationOrder"] = order;
        row["OperationType"] = 1;
        row["LastModifiedOnClient"] = 1646000000L;
        row.TryAdd("CreatedTime", 1646000100L);
        return row;
    }

    /// <summary>A database at Users/user/L.user/ActivitiesCache.db with the Windows 11 schema (or <paramref name="schema" />) and these rows</summary>
    protected string Database(IEnumerable<Dictionary<string, object>> activities, IEnumerable<Dictionary<string, object>> operations = null,
        string schema = Schema)
    {
        var folder = Path.Combine(Dir, "Users", "user", "L.user");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "ActivitiesCache.db");

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            connection.Open();
            Execute(connection, schema);

            Insert(connection, "Activity", activities);
            Insert(connection, "ActivityOperation", operations ?? Array.Empty<Dictionary<string, object>>());
        }

        return path;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void Insert(SqliteConnection connection, string table, IEnumerable<Dictionary<string, object>> rows)
    {
        foreach (var row in rows)
        {
            using var cmd = connection.CreateCommand();
            var columns = row.Keys.ToList();
            cmd.CommandText = $"INSERT INTO [{table}] ({string.Join(",", columns.Select(c => $"[{c}]"))}) VALUES ({string.Join(",", columns.Select((_, i) => $"$p{i}"))})";
            for (var i = 0; i < columns.Count; i++)
            {
                cmd.Parameters.AddWithValue($"$p{i}", row[columns[i]] ?? DBNull.Value);
            }

            cmd.ExecuteNonQuery();
        }
    }
}
