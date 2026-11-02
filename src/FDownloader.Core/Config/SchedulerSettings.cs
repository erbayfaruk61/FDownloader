using System;

namespace FDownloader.Core.Config;

public enum SchedulerStartAction
{
    StartAllDownloads = 0,
    DisableSpeedLimits = 1
}

public enum SchedulerEndAction
{
    PauseAllDownloads = 0,
    EnableSpeedLimits = 1,
    ShutdownComputer = 2,
    SleepComputer = 3,
    DoNothing = 4
}

public sealed class SchedulerSettings
{
    public bool IsEnabled { get; set; } = false;
    public string StartTime { get; set; } = "02:00";
    public string EndTime { get; set; } = "07:00";
    public SchedulerStartAction StartAction { get; set; } = SchedulerStartAction.StartAllDownloads;
    public SchedulerEndAction EndAction { get; set; } = SchedulerEndAction.PauseAllDownloads;

    // Günler
    public bool Monday { get; set; } = true;
    public bool Tuesday { get; set; } = true;
    public bool Wednesday { get; set; } = true;
    public bool Thursday { get; set; } = true;
    public bool Friday { get; set; } = true;
    public bool Saturday { get; set; } = true;
    public bool Sunday { get; set; } = true;

    public bool IsDayActive(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        DayOfWeek.Saturday => Saturday,
        DayOfWeek.Sunday => Sunday,
        _ => false
    };
}

