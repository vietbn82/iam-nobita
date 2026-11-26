using System.Globalization;
using Microsoft.Data.Sqlite;

namespace IAmNobita;

/// <summary>Mode-switch log in SQLite. Pure (no WinForms) so tests can link it.</summary>
sealed class History(string path)
{
    const string TimeFormat = "yyyy-MM-dd HH:mm:ss"; // local time; sorts as text

    public sealed record Entry(DateTime At, bool Nobita, string Source);

    public static readonly History Default = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IAmNobita", "history.db"));

    public void Add(DateTime at, bool nobita, string source)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO log(at, nobita, source) VALUES ($at, $nobita, $source)";
        cmd.Parameters.AddWithValue("$at", at.ToString(TimeFormat, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$nobita", nobita);
        cmd.Parameters.AddWithValue("$source", source);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Entries with from &lt;= At &lt; toExclusive, newest first.</summary>
    public List<Entry> Query(DateTime from, DateTime toExclusive)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT at, nobita, source FROM log WHERE at >= $from AND at < $to ORDER BY at DESC, id DESC";
        cmd.Parameters.AddWithValue("$from", from.ToString(TimeFormat, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$to", toExclusive.ToString(TimeFormat, CultureInfo.InvariantCulture));
        using var r = cmd.ExecuteReader();
        var list = new List<Entry>();
        while (r.Read())
            list.Add(new(DateTime.ParseExact(r.GetString(0), TimeFormat, CultureInfo.InvariantCulture), r.GetBoolean(1), r.GetString(2)));
        return list;
    }

    SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS log(id INTEGER PRIMARY KEY, at TEXT NOT NULL, nobita INTEGER NOT NULL, source TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS log_at ON log(at);
            """;
        cmd.ExecuteNonQuery();
        return db;
    }
}
