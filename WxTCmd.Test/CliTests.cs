using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace WxTCmd.Test;

/// <summary>The built WxTCmd.dll, run as a separate process</summary>
[TestFixture]
public class CliTests : TestData
{
    private (int ExitCode, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Dir
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "WxTCmd.dll"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Close();

        process.WaitForExit(300_000).Should().BeTrue("WxTCmd should finish");
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private string Out => Path.Combine(Dir, "out");

    private string Csv(string suffix) => Directory.GetFiles(Out, "*" + suffix).Single();

    private string[] Header(string suffix) => File.ReadLines(Csv(suffix)).First().TrimStart('﻿').Split(',');

    private static readonly string[] ActivityColumns2026_5 =
    {
        "Id", "ActivityTypeOrg", "ActivityType", "Executable", "DisplayText", "ContentInfo", "Payload", "ClipboardPayload", "StartTime", "EndTime",
        "Duration", "LastModifiedTime", "LastModifiedOnClient", "OriginalLastModifiedOnClient", "ExpirationTime", "CreatedInCloud", "IsLocalOnly",
        "ETag", "PackageIdHash", "PlatformDeviceId", "DevicePlatform", "TimeZone"
    };

    private static readonly string[] OperationColumns2026_5 =
    {
        "Id", "ActivityTypeOrg", "ActivityType", "Executable", "DisplayText", "ContentInfo", "Payload", "ClipboardPayload", "StartTime", "EndTime",
        "Duration", "LastModifiedTime", "LastModifiedTimeOnClient", "CreatedTime", "ExpirationTime", "OperationExpirationTime", "OperationOrder",
        "AppId", "OperationType", "Description", "PlatformDeviceId", "DevicePlatform", "TimeZone"
    };

    [Test]
    public void Sample()
    {
        var db = Sample("DFIRArtifactMuseum/Win11/RathbunVM");
        var before = Hashes(Path.GetDirectoryName(db)!);

        var (code, output) = Run("-f", db, "--csv", Out);

        code.Should().Be(0, output);
        output.Should().Contain("Activity entries found: 30").And.Contain("Activity_PackageId entries found: 80").And.Contain("ActivityOperation entries found: 0");
        Hashes(Path.GetDirectoryName(db)!).Should().BeEquivalentTo(before);

        //the user name from the path (Users/user)
        Directory.GetFiles(Out).Select(t => Path.GetFileName(t)[14..]).Should().BeEquivalentTo("_user_Activity.csv", "_user_Activity_PackageIDs.csv");

        //2026.5's columns in their order, then the new ones
        Header("_Activity.csv").Should().Equal(ActivityColumns2026_5.Concat(new[] { "AppActivityId", "ClipboardText" }));
        Header("_Activity_PackageIDs.csv").Should().Equal("Id", "Platform", "Name", "AdditionalInformation", "Expires");
        File.ReadLines(Csv("_Activity.csv"), Encoding.UTF8).Should().HaveCount(31);
    }

    [Test]
    public void Operations()
    {
        var db = Database(new[] { ActivityRow(Guid.NewGuid()) }, new[] { OperationRow(1, ("EndTime", 1646000065L)) });

        var (code, output) = Run("-f", db, "--csv", Out, "--dt", "yyyy-MM-dd'T'HH:mm:ss");

        code.Should().Be(0, output);
        Header("_ActivityOperations.csv").Should().Equal(OperationColumns2026_5.Concat(new[] { "AppActivityId", "ClipboardText" }));

        var line = File.ReadLines(Csv("_ActivityOperations.csv")).Skip(1).Single();
        line.Should().Contain(@"ProgramFilesX64\Notepad++\notepad++.exe").And.Contain("2022-02-27T22:13:20,2022-02-27T22:14:25,00:01:05");
    }

    [Test]
    public void BadValuesAreListed()
    {
        var db = Database(new[] { ActivityRow(Guid.NewGuid()), ActivityRow(Guid.NewGuid(), ("AppId", "not json")) });

        var (code, output) = Run("-f", db, "--csv", Out);

        code.Should().Be(0, output);
        output.Should().Contain("Activity row 2: AppId:");
        File.ReadLines(Csv("_Activity.csv")).Should().HaveCount(3);
    }

    [Test]
    public void MissingArguments()
    {
        Run("--csv", Out).ExitCode.Should().Be(1);
        Run("-f", Path.Combine(Dir, "nope.db")).ExitCode.Should().Be(1);
        Run("-f", Path.Combine(Dir, "nope.db"), "--csv", Out).ExitCode.Should().Be(1);
    }

    [Test]
    public void NotADatabase()
    {
        var path = Path.Combine(Dir, "ActivitiesCache.db");
        File.WriteAllText(path, new string('x', 5000));

        var (code, output) = Run("-f", path, "--csv", Out);

        code.Should().Be(1, output);
        output.Should().Contain("not a SQLite database");
    }

    [Test]
    public void OtherDatabase()
    {
        var db = Database(Array.Empty<System.Collections.Generic.Dictionary<string, object>>(), schema: "CREATE TABLE t(a);");

        var (code, output) = Run("-f", db, "--csv", Out);

        code.Should().Be(1, output);
        output.Should().Contain("Is it an ActivitiesCache.db?");
    }

    [Test]
    public void NothingInTheWorkingFolder()
    {
        //the old version wrote SQLite.Interop.dll / libSQLite.Interop.so into the current folder
        var db = Sample("plaso");

        Run("-f", db, "--csv", Out).ExitCode.Should().Be(0);

        Directory.GetFiles(Dir).Should().BeEmpty();
    }
}
