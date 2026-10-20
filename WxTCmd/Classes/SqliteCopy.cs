using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WxTCmd.Classes;

/// <summary>
///     A database copied (with its -wal and -journal) to a new folder in the temporary folder (--temp, or Temp next to
///     WxTCmd: <see cref="TempFolder" />), deleted when disposed. Opening the evidence itself would checkpoint the WAL
///     into it and delete the -wal and -shm files
/// </summary>
internal sealed class SqliteCopy : IDisposable
{
    private readonly string _folder;

    private SqliteCopy(string folder, SqliteConnection connection)
    {
        _folder = folder;
        Connection = connection;
    }

    public SqliteConnection Connection { get; }

    /// <param name="path">The database</param>
    /// <param name="tempFolder">Where the copy's folder is made (null: the system's temp folder, for the tests)</param>
    public static SqliteCopy Open(string path, string tempFolder = null)
    {
        var folder = Path.Combine(tempFolder ?? Path.GetTempPath(), "WxTCmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var copy = Path.Combine(folder, Path.GetFileName(path));
            foreach (var suffix in new[] { "", "-wal", "-journal" })
            {
                if (File.Exists(path + suffix))
                {
                    CopyShared(path + suffix, copy + suffix);
                }
            }

            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = copy,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ToString());
            connection.Open();

            return new SqliteCopy(folder, connection);
        }
        catch
        {
            TryDelete(folder);
            throw;
        }
    }

    public void Dispose()
    {
        Connection.Dispose();
        TryDelete(_folder);
    }

    /// <summary>The database is in use on a live system</summary>
    private static void CopyShared(string source, string target)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
        input.CopyTo(output);
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, true);
        }
        catch (Exception)
        {
            //a temp folder left behind is not worth failing for
        }
    }
}
