using IAmNobita;
using Microsoft.Data.Sqlite;
using Xunit;

public class HistoryTests
{
    [Fact]
    public void Query_filters_by_range_newest_first()
    {
        var path = Path.Combine(Path.GetTempPath(), $"iamnobita-test-{Guid.NewGuid():N}.db");
        try
        {
            var h = new History(path);
            h.Add(new DateTime(2026, 11, 1, 9, 0, 0), true, "Hotkey");
            h.Add(new DateTime(2026, 11, 10, 8, 30, 5), false, "Lịch \"Sáng\"");
            h.Add(new DateTime(2026, 11, 10, 17, 0, 0), true, "Hẹn giờ");
            h.Add(new DateTime(2026, 11, 20, 12, 0, 0), false, "Menu");

            var got = h.Query(new DateTime(2026, 11, 2), new DateTime(2026, 11, 11));

            Assert.Equal(
                [new History.Entry(new DateTime(2026, 11, 10, 17, 0, 0), true, "Hẹn giờ"), new History.Entry(new DateTime(2026, 11, 10, 8, 30, 5), false, "Lịch \"Sáng\"")],
                got);

            h.Purge(new DateTime(2026, 11, 10, 12, 0, 0));
            Assert.Equal(2, h.Query(DateTime.MinValue, DateTime.MaxValue).Count); // 10/11 17:00 + 20/11 kept
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
