using System;

namespace WxTCmd.Classes;

/// <summary>A row of the Activity_PackageId table</summary>
public class ActivityPackageIdEntry
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

    public string Id { get; set; }
    public string Platform { get; set; }
    public string Name { get; set; }
    public string AdditionalInformation { get; set; }

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
