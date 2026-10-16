using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using WxTCmd.Classes;

namespace WxTCmd.Test;

/// <summary>
///     Every ActivitiesCache.db in Corpus/WindowsTimeline next to the repository (or WXTCMD_CORPUS). Never copy those
///     files into the repository
/// </summary>
[TestFixture]
[Explicit("needs the local corpus")]
public class CorpusTests : TestData
{
    private static string CorpusFolder()
    {
        var env = Environment.GetEnvironmentVariable("WXTCMD_CORPUS");
        if (string.IsNullOrEmpty(env) == false)
        {
            return env;
        }

        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            foreach (var candidate in new[] { Path.Combine(dir.FullName, "Corpus", "WindowsTimeline"), Path.Combine(dir.FullName, "WindowsTimeline") })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    [Test]
    public void EveryDatabase()
    {
        var corpus = CorpusFolder();
        if (corpus == null)
        {
            Assert.Ignore("Corpus/WindowsTimeline not found");
        }

        var files = Directory.GetFiles(corpus, "ActivitiesCache.db", SearchOption.AllDirectories);
        files.Should().NotBeEmpty();

        foreach (var file in files)
        {
            var before = Hashes(Path.GetDirectoryName(file)!);

            var data = ActivitiesCache.Read(file);

            TestContext.Out.WriteLine($"{file}: {data.Activities.Count} activities, {data.Operations.Count} operations, {data.PackageIds.Count} package ids");
            data.HasTimelineTables.Should().BeTrue(file);
            data.TableErrors.Should().BeEmpty(file);
            data.ParseErrors.Should().BeEmpty(file);
            Hashes(Path.GetDirectoryName(file)!).Should().BeEquivalentTo(before, file);
        }
    }
}
