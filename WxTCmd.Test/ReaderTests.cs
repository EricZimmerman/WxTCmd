using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using WxTCmd.Classes;

namespace WxTCmd.Test;

[TestFixture]
public class ReaderTests : TestData
{
    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    [Test]
    public void Win11WalOnly()
    {
        //the database file is one empty page: every row is in the WAL
        var db = Sample("DFIRArtifactMuseum/Win11/RathbunVM");
        var before = Hashes(Path.GetDirectoryName(db)!);

        var data = ActivitiesCache.Read(db);

        data.Activities.Should().HaveCount(30);
        data.PackageIds.Should().HaveCount(80);
        data.Operations.Should().BeEmpty();
        data.MissingTables.Should().BeEmpty();
        data.TableErrors.Should().BeEmpty();
        data.ParseErrors.Should().BeEmpty();
        data.QuickCheck.Should().Be("ok");

        //the evidence is untouched: the WAL is not checkpointed into it and no -shm is made or deleted
        Hashes(Path.GetDirectoryName(db)!).Should().BeEquivalentTo(before);

        var notepad = data.Activities.Single(t => t.ContentInfo.StartsWith(@"C:\temp.txt"));
        //the old version cut it at fixed offsets: "file:Unmapped GUID: //C:/temp.txt?VolumeId={1147D6EA-BCA-41CB-..."
        notepad.ContentInfo.Should().StartWith(@"C:\temp.txt (file:///C:/temp.txt?VolumeId=").And.Contain("VolumeId={1147D6EA-BCAE-41CB-A7E1-5A41C07250FE}");
        notepad.DisplayText.Should().Be("temp.txt (Notepad)");
    }

    [Test]
    public void Utf8Payloads()
    {
        var data = ActivitiesCache.Read(Sample("DFIRArtifactMuseum/Win10/BelkasoftCTF_InsiderThreat_L.User"));

        data.Activities.Should().HaveCount(62);
        data.PackageIds.Should().HaveCount(169);
        data.Activities.Select(t => t.DisplayText).Should().Contain("MSN Россия | Новости, фото, погода, Skype и Outlook - Internet Explorer");

        //no " ()" when there is no appDisplayName
        data.Activities.Select(t => t.DisplayText).Should().Contain("chrome - Bing - Internet Explorer");

        var focus = data.Activities.Single(t => t.Id == "ba041f9d-cc50-9f46-3398-bfff5a19eaaa");
        focus.ActivityType.Should().Be(ActivityEntry.ActivityTypes.InFocus);
        focus.Executable.Should().Be("windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel");
        focus.StartTime.Should().Be(new DateTimeOffset(2021, 2, 13, 19, 57, 9, TimeSpan.Zero));
        focus.Duration.Should().Be(TimeSpan.FromSeconds(5));
        focus.TimeZone.Should().Be("America/Los_Angeles");
        focus.CreatedInCloud.Should().BeNull();
    }

    [Test]
    public void OldestSchema()
    {
        //Windows 10 1803: no UserActionState (the old version lost the whole Activity table on it)
        var data = ActivitiesCache.Read(Sample("plaso"));

        data.Activities.Should().HaveCount(112);
        data.PackageIds.Should().HaveCount(303);
        data.ParseErrors.Should().BeEmpty();
        data.TableErrors.Should().BeEmpty();
    }

    [Test]
    public void Operations()
    {
        var clipboard = $"[{{\"content\":\"{Base64("copied text: Привет")}\",\"formatName\":\"Text\"}}]";

        var db = Database(new[] { ActivityRow(Guid.NewGuid()) }, new[]
        {
            OperationRow(1, ("EndTime", 1646000065L)),
            OperationRow(2, ("ActivityType", 10), ("ClipboardPayload", Encoding.UTF8.GetBytes(clipboard)),
                ("Payload", "{\"gdprType\":\"ProductAndServiceUsage\"}"u8.ToArray()))
        });

        var data = ActivitiesCache.Read(db);

        data.Operations.Should().HaveCount(2);
        data.ParseErrors.Should().BeEmpty();

        var first = data.Operations.Single(t => t.OperationOrder == 1);
        first.Executable.Should().Be(@"ProgramFilesX64\Notepad++\notepad++.exe");
        first.AppActivityId.Should().Be("ECB32AF3-1440-4086-94E3-5311F97F89C4");
        first.Duration.Should().Be(TimeSpan.FromSeconds(65));
        first.CreatedTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1646000100));
        first.OperationExpirationTime.Should().BeNull();

        var clip = data.Operations.Single(t => t.OperationOrder == 2);
        clip.ActivityType.Should().Be(ActivityEntry.ActivityTypes.CloudClipboard);
        clip.ClipboardPayload.Should().Be(clipboard);
        clip.ClipboardText.Should().Be("copied text: Привет");
    }

    [TestCase("[{\"content\":\"aGVsbG8=\",\"formatName\":\"Text\"}]", "hello")]
    [TestCase("[{\"content\":\"aGVsbG8=\"}]", "hello")]
    [TestCase("[{\"content\":\"aGVsbG8=\",\"formatName\":\"Text\"},{\"content\":\"d29ybGQ=\",\"formatName\":\"UnicodeText\"}]", "hello | world")]
    [TestCase("[{\"content\":\"/9j/4AAQ\",\"formatName\":\"Bitmap\"}]", "")]
    [TestCase("[{\"content\":\"/9j/4AAQ\",\"formatName\":\"Text\"}]", "")] //not UTF-8
    [TestCase("[{\"content\":\"not base64!\",\"formatName\":\"Text\"}]", "")]
    [TestCase("[1, \"x\", null]", "")]
    [TestCase("", "")]
    [TestCase("binary", "")]
    public void ClipboardText(string payload, string expected)
    {
        ActivitiesCache.ClipboardText(payload).Should().Be(expected);
    }

    [Test]
    public void ContentUris()
    {
        ActivitiesCache.ContentUri("file:///C:/temp/a%20b.txt?VolumeId={A78E0A99-2928-4625-90CF-E0B88E88439B}")
            .Should().Be("file:///C:/temp/a b.txt?VolumeId={A78E0A99-2928-4625-90CF-E0B88E88439B}");
        ActivitiesCache.ContentUri("file:{374DE290-123F-4565-9164-39C4925E467B}\\a.zip").Should().Be("file:Downloads\\a.zip");
        ActivitiesCache.ContentUri("file:///{374DE290-123F-4565-9164-39C4925E467B}\\a.zip").Should().Be("file:///Downloads\\a.zip");
        ActivitiesCache.ContentUri("file:{00000000-0000-0000-0000-00000000ABCD}\\a.zip").Should().Be("file:{00000000-0000-0000-0000-00000000ABCD}\\a.zip");
        ActivitiesCache.ContentUri("{").Should().Be("{");
        ActivitiesCache.ContentUri(null).Should().BeNull();
    }

    [Test]
    public void Executables()
    {
        ActivitiesCache.Executable(AppId).Should().Be(@"ProgramFilesX64\Notepad++\notepad++.exe");
        ActivitiesCache.Executable("[{\"application\":\"Microsoft.Windows.Explorer\",\"platform\":\"windows_universal\"},{\"application\":\"x\",\"platform\":\"host\"}]")
            .Should().Be("Microsoft.Windows.Explorer");
        ActivitiesCache.Executable("[{\"application\":\"a\",\"platform\":\"host\"}]").Should().Be("a");
        ActivitiesCache.Executable("[{\"application\":\"{00000000-0000-0000-0000-00000000ABCD}\\\\a.exe\",\"platform\":\"x_exe_path\"}]")
            .Should().Be(@"{00000000-0000-0000-0000-00000000ABCD}\a.exe");
        ActivitiesCache.Executable("[]").Should().Be("");
        ActivitiesCache.Executable("{}").Should().Be("");
    }

    [Test]
    public void BadValues()
    {
        var good = Guid.NewGuid();
        var db = Database(new[]
        {
            ActivityRow(good),
            //each of these used to lose the whole table
            ActivityRow(Guid.NewGuid(), ("AppId", "not json")),
            ActivityRow(Guid.NewGuid(), ("AppId", "[]")),
            ActivityRow(Guid.NewGuid(), ("Payload", "{\"displayText\": broken"u8.ToArray())),
            ActivityRow(Guid.NewGuid(), ("Payload", "{\"contentUri\":\"file:{x\",\"description\":\"d\"}"u8.ToArray())),
            ActivityRow(Guid.NewGuid(), ("StartTime", "yesterday"), ("ETag", "many"), ("EndTime", long.MaxValue)),
            ActivityRow(Guid.NewGuid(), ("ClipboardPayload", "[{\"content\": 5"u8.ToArray())),
            ActivityRow(Guid.NewGuid(), ("Id", new byte[] { 1, 2, 3 }), ("Payload", null))
        });

        var data = ActivitiesCache.Read(db);

        data.Activities.Should().HaveCount(8);
        data.TableErrors.Should().BeEmpty();
        data.ParseErrors.Should().HaveCount(6);
        data.ParseErrors.Should().Contain(t => t.Contains("StartTime: 'yesterday' is not a time"));
        data.ParseErrors.Should().Contain(t => t.Contains("EndTime: 9223372036854775807 is out of range"));

        data.Activities.Single(t => t.Id == good.ToString()).Executable.Should().Be(@"ProgramFilesX64\Notepad++\notepad++.exe");
        data.Activities.Should().Contain(t => t.Id == "010203");
        data.Activities.Single(t => t.ContentInfo == "d (file:{x)").Should().NotBeNull();
        data.Activities.Single(t => t.Payload.StartsWith("{\"displayText\": broken")).DisplayText.Should().BeEmpty();
    }

    [Test]
    public void NotADatabase()
    {
        var path = Path.Combine(Dir, "ActivitiesCache.db");
        File.WriteAllBytes(path, Enumerable.Range(0, 8192).Select(i => (byte)i).ToArray());

        FluentActions.Invoking(() => ActivitiesCache.Read(path)).Should().Throw<InvalidDataException>();
    }

    [Test]
    public void OtherDatabase()
    {
        var data = ActivitiesCache.Read(Database(Array.Empty<System.Collections.Generic.Dictionary<string, object>>(), schema: "CREATE TABLE t(a);"));

        data.HasTimelineTables.Should().BeFalse();
        data.MissingTables.Should().BeEquivalentTo("Activity", "ActivityOperation", "Activity_PackageId");
    }

    [Test]
    public void TruncatedDatabase()
    {
        var db = Database(Enumerable.Range(0, 400).Select(_ => ActivityRow(Guid.NewGuid(), ("Payload", Encoding.UTF8.GetBytes("{\"displayText\":\"" + new string('x', 900) + "\"}")))));
        var bytes = File.ReadAllBytes(db);
        File.WriteAllBytes(db, bytes.Take(bytes.Length / 2).ToArray());

        var data = ActivitiesCache.Read(db);

        //the rows before the damage are kept
        data.QuickCheck.Should().NotBe("ok");
        data.TableErrors.Should().ContainSingle().Which.Should().StartWith("Activity: error after");
        data.Activities.Should().HaveCountGreaterThan(100).And.HaveCountLessThan(400);
    }

    /// <summary>Random damage to the samples: no exception but InvalidDataException, and no hang. WXTCMD_FUZZ_ROUNDS sets the rounds</summary>
    [TestCase("DFIRArtifactMuseum/Win10/BelkasoftCTF_InsiderThreat_L.User")]
    [TestCase("plaso")]
    public void RandomDamage(string sample)
    {
        var db = Sample(sample, "orig");
        var original = File.ReadAllBytes(db);
        var random = new Random(1234);

        var rounds = int.TryParse(Environment.GetEnvironmentVariable("WXTCMD_FUZZ_ROUNDS"), out var r) ? r : 150;
        for (var round = 0; round < rounds; round++)
        {
            var bytes = (byte[])original.Clone();
            var count = random.Next(1, 200);
            for (var i = 0; i < count; i++)
            {
                //the header (first 100 bytes) less often
                var at = random.Next(i % 10 == 0 ? 0 : 100, bytes.Length);
                bytes[at] = (byte)random.Next(256);
            }

            var path = Path.Combine(Dir, $"damaged{round}.db");
            File.WriteAllBytes(path, bytes);

            var sw = Stopwatch.StartNew();
            try
            {
                ActivitiesCache.Read(path);
            }
            catch (InvalidDataException)
            {
            }

            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), $"round {round}");
            File.Delete(path);
        }
    }
}
