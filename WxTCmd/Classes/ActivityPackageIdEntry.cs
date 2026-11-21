using System;
using CsvHelper;

namespace WxTCmd.Classes;

/// <summary>
///     A row of the Activity_PackageId table; its columns in the Activity_PackageIDs CSV, Expires in the --dt format. The
///     columns are named because CsvHelper would otherwise take the names of the constructor's parameters (id, platform...)
/// </summary>
[CsvRecord]
public partial class ActivityPackageIdEntry
{
    public ActivityPackageIdEntry(string id, string platform, string name, string additionalInformation,
        DateTimeOffset? expires)
    {
        Id = id;
        Platform = platform switch
        {
            "windows_win32" => "Win32",
            "x_exe_path" => "ExecutablePath",
            "packageId" => "Package",
            _ => platform
        };

        Name = name;
        AdditionalInformation = additionalInformation;
        Expires = expires;
    }

    [CsvColumn("Id", Index = 0)] public string Id { get; set; }
    [CsvColumn("Platform", Index = 1)] public string Platform { get; set; }
    [CsvColumn("Name", Index = 2)] public string Name { get; set; }
    [CsvColumn("AdditionalInformation", Index = 3)] public string AdditionalInformation { get; set; }

    [CsvColumn("Expires", Index = 4)]
    [CsvConverter(typeof(DateTimeFormatConverter))]
    public DateTimeOffset? Expires { get; set; }

    public override string ToString()
    {
        var addlInfo = string.Empty;

        if (AdditionalInformation?.Length > 0)
        {
            addlInfo = $" Additional info: {AdditionalInformation}";
        }

        return $"Platform: {Platform} Name: {Name} Expires: {Expires}{addlInfo}";
    }
}
