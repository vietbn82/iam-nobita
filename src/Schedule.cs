namespace IAmNobita;

/// <summary>"At <see cref="Time"/> on <see cref="Days"/>, set mode to Nobita (true) or Cống hiến (false)."</summary>
sealed record ScheduleRule
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public TimeOnly Time { get; set; }
    public bool Nobita { get; set; }
    public DayOfWeek[] Days { get; set; } = [];
}

static class Schedule
{
    /// <summary>
    /// Latest enabled rule whose occurrence falls in (prev, now]. Called every tick with the previous tick's time,
    /// so a rule fires once, never retroactively at app start, and after sleep the most recent missed rule wins.
    /// Dates in <paramref name="skip"/> (holidays) never fire. Null when nothing is due or the clock went backwards.
    /// </summary>
    public static ScheduleRule? Due(IEnumerable<ScheduleRule> rules, DateTime prev, DateTime now, ICollection<DateOnly>? skip = null)
    {
        if (now <= prev) return null;
        var from = prev.Date < now.Date.AddDays(-7) ? now.Date.AddDays(-7) : prev.Date; // ponytail: weekly rules, 7 days back is enough
        ScheduleRule? due = null;
        var dueAt = DateTime.MinValue;
        for (var day = from; day <= now.Date; day = day.AddDays(1))
        {
            if (skip?.Contains(DateOnly.FromDateTime(day)) == true) continue;
            foreach (var rule in rules)
            {
                if (!rule.Enabled || !rule.Days.Contains(day.DayOfWeek)) continue;
                var at = day + rule.Time.ToTimeSpan();
                if (at > prev && at <= now && at >= dueAt) (due, dueAt) = (rule, at);
            }
        }
        return due;
    }
}
