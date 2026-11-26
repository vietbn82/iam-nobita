using IAmNobita;
using Xunit;

public class ScheduleTests
{
    static readonly DateTime Monday = new(2026, 11, 23);
    static readonly DayOfWeek[] Weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    static ScheduleRule Rule(string time, bool nobita, DayOfWeek[]? days = null, bool enabled = true) =>
        new() { Name = time, Time = TimeOnly.Parse(time), Nobita = nobita, Days = days ?? Weekdays, Enabled = enabled };

    static DateTime At(DateTime day, string time) => day + TimeSpan.Parse(time);

    [Fact]
    public void MondayIsMonday() => Assert.Equal(DayOfWeek.Monday, Monday.DayOfWeek);

    [Fact]
    public void FiresWhenCrossingTime()
    {
        var rule = Rule("08:30", true);
        Assert.Same(rule, Schedule.Due([rule], At(Monday, "08:29:59"), At(Monday, "08:30:00")));
    }

    [Fact]
    public void DoesNotFireTwice()
    {
        var rule = Rule("08:30", true);
        Assert.Null(Schedule.Due([rule], At(Monday, "08:30:00"), At(Monday, "08:30:01")));
    }

    [Fact]
    public void WrongDayDoesNotFire()
    {
        var saturday = Monday.AddDays(5);
        Assert.Null(Schedule.Due([Rule("08:30", true)], At(saturday, "08:29:59"), At(saturday, "08:30:00")));
    }

    [Fact]
    public void AppStartedMidDayDoesNotFirePastRules()
    {
        Assert.Null(Schedule.Due([Rule("08:30", true)], At(Monday, "15:00:00"), At(Monday, "15:00:01")));
    }

    [Fact]
    public void FiresAcrossMidnight()
    {
        var rule = Rule("00:00", true, [DayOfWeek.Tuesday]);
        Assert.Same(rule, Schedule.Due([rule], At(Monday, "23:59:59"), At(Monday.AddDays(1), "00:00:00")));
    }

    [Fact]
    public void AfterSleepLatestMissedRuleWins()
    {
        var morning = Rule("08:30", false);
        var lunch = Rule("12:00", true);
        Assert.Same(lunch, Schedule.Due([lunch, morning], At(Monday, "07:00"), At(Monday, "13:00")));
    }

    [Fact]
    public void ClockGoingBackwardsDoesNotFire()
    {
        Assert.Null(Schedule.Due([Rule("08:30", true)], At(Monday, "08:31"), At(Monday, "08:29")));
    }

    [Fact]
    public void DisabledRuleDoesNotFire()
    {
        Assert.Null(Schedule.Due([Rule("08:30", true, enabled: false)], At(Monday, "08:29:59"), At(Monday, "08:30:00")));
    }
}
