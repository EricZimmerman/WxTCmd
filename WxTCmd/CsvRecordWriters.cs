using System;
using System.Globalization;
using CsvHelper;
using WxTCmd.Classes;

namespace WxTCmd;

/// <summary>
///     Writes a time stamp in the --dt format (<see cref="Format" />, set once at startup), with the invariant culture;
///     no time stamp is an empty field
/// </summary>
internal sealed class DateTimeFormatConverter : ICsvConverter<DateTimeOffset>
{
    public static string Format { get; set; } = "yyyy-MM-dd HH:mm:ss";

    public DateTimeOffset Read(CsvReader reader, int index) =>
        DateTimeOffset.ParseExact(reader.GetString(index), Format, CultureInfo.InvariantCulture);

    public void Write(CsvWriter writer, DateTimeOffset value) => writer.WriteField(Time(value));

    public static string Time(DateTimeOffset? time) => time?.ToString(Format, CultureInfo.InvariantCulture) ?? "";
}

/// <summary>
///     The columns of the ActivityOperations CSV (2026.5's, in their order; new columns go at the end). Written by hand
///     because ActivityType and Duration are computed, and CsvHelper 34 record types write only members that can be set
/// </summary>
internal sealed class ActivityOperationCsvWriter : CsvRecordWriter<ActivityOperationEntry>
{
    private static readonly string[] Columns =
    {
        "Id", "ActivityTypeOrg", "ActivityType", "Executable", "DisplayText", "ContentInfo", "Payload", "ClipboardPayload",
        "StartTime", "EndTime", "Duration", "LastModifiedTime", "LastModifiedTimeOnClient", "CreatedTime", "ExpirationTime",
        "OperationExpirationTime", "OperationOrder", "AppId", "OperationType", "Description", "PlatformDeviceId",
        "DevicePlatform", "TimeZone", "AppActivityId", "ClipboardText"
    };

    public override int FieldCount => Columns.Length;

    public override void WriteHeader(CsvWriter writer)
    {
        foreach (var column in Columns)
        {
            WriteColumnName(writer, column);
        }
    }

    public override void Write(CsvWriter writer, ActivityOperationEntry record)
    {
        writer.WriteField(record.Id);
        writer.WriteField(record.ActivityTypeOrg);
        writer.WriteField(record.ActivityType.ToString());
        writer.WriteField(record.Executable);
        writer.WriteField(record.DisplayText);
        writer.WriteField(record.ContentInfo);
        writer.WriteField(record.Payload);
        writer.WriteField(record.ClipboardPayload);
        writer.WriteField(DateTimeFormatConverter.Time(record.StartTime));
        writer.WriteField(DateTimeFormatConverter.Time(record.EndTime));
        writer.WriteField(record.Duration);
        writer.WriteField(DateTimeFormatConverter.Time(record.LastModifiedTime));
        writer.WriteField(DateTimeFormatConverter.Time(record.LastModifiedTimeOnClient));
        writer.WriteField(DateTimeFormatConverter.Time(record.CreatedTime));
        writer.WriteField(DateTimeFormatConverter.Time(record.ExpirationTime));
        writer.WriteField(DateTimeFormatConverter.Time(record.OperationExpirationTime));
        writer.WriteField(record.OperationOrder);
        writer.WriteField(record.AppId);
        writer.WriteField(record.OperationType);
        writer.WriteField(record.Description);
        writer.WriteField(record.PlatformDeviceId);
        writer.WriteField(record.DevicePlatform);
        writer.WriteField(record.TimeZone);
        writer.WriteField(record.AppActivityId);
        writer.WriteField(record.ClipboardText);
    }
}

/// <summary>
///     The columns of the Activity CSV (2026.5's, in their order; new columns go at the end). Written by hand because
///     ActivityType and Duration are computed, and CsvHelper 34 record types write only members that can be set
/// </summary>
internal sealed class ActivityCsvWriter : CsvRecordWriter<ActivityEntry>
{
    private static readonly string[] Columns =
    {
        "Id", "ActivityTypeOrg", "ActivityType", "Executable", "DisplayText", "ContentInfo", "Payload", "ClipboardPayload",
        "StartTime", "EndTime", "Duration", "LastModifiedTime", "LastModifiedOnClient", "OriginalLastModifiedOnClient",
        "ExpirationTime", "CreatedInCloud", "IsLocalOnly", "ETag", "PackageIdHash", "PlatformDeviceId", "DevicePlatform",
        "TimeZone", "AppActivityId", "ClipboardText"
    };

    public override int FieldCount => Columns.Length;

    public override void WriteHeader(CsvWriter writer)
    {
        foreach (var column in Columns)
        {
            WriteColumnName(writer, column);
        }
    }

    public override void Write(CsvWriter writer, ActivityEntry record)
    {
        writer.WriteField(record.Id);
        writer.WriteField(record.ActivityTypeOrg);
        writer.WriteField(record.ActivityType.ToString());
        writer.WriteField(record.Executable);
        writer.WriteField(record.DisplayText);
        writer.WriteField(record.ContentInfo);
        writer.WriteField(record.Payload);
        writer.WriteField(record.ClipboardPayload);
        writer.WriteField(DateTimeFormatConverter.Time(record.StartTime));
        writer.WriteField(DateTimeFormatConverter.Time(record.EndTime));
        writer.WriteField(record.Duration);
        writer.WriteField(DateTimeFormatConverter.Time(record.LastModifiedTime));
        writer.WriteField(DateTimeFormatConverter.Time(record.LastModifiedOnClient));
        writer.WriteField(DateTimeFormatConverter.Time(record.OriginalLastModifiedOnClient));
        writer.WriteField(DateTimeFormatConverter.Time(record.ExpirationTime));
        writer.WriteField(DateTimeFormatConverter.Time(record.CreatedInCloud));
        writer.WriteField(record.IsLocalOnly);
        writer.WriteField(record.ETag);
        writer.WriteField(record.PackageIdHash);
        writer.WriteField(record.PlatformDeviceId);
        writer.WriteField(record.DevicePlatform);
        writer.WriteField(record.TimeZone);
        writer.WriteField(record.AppActivityId);
        writer.WriteField(record.ClipboardText);
    }
}
