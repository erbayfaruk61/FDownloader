using FDownloader.Core.Config;
using Xunit;

namespace FDownloader.Core.Tests;

public class SchedulerSettingsTests
{
    [Fact]
    public void SchedulerSettings_DefaultValues_AreCorrect()
    {
        var settings = new SchedulerSettings();

        Assert.False(settings.IsEnabled);
        Assert.Equal("02:00", settings.StartTime);
        Assert.Equal("07:00", settings.EndTime);
        Assert.Equal(SchedulerStartAction.StartAllDownloads, settings.StartAction);
        Assert.Equal(SchedulerEndAction.PauseAllDownloads, settings.EndAction);
        Assert.True(settings.Monday);
        Assert.True(settings.Sunday);
    }

    [Fact]
    public void SchedulerSettings_IsDayActive_RespectsConfiguration()
    {
        var settings = new SchedulerSettings
        {
            Monday = true,
            Wednesday = true,
            Friday = false,
            Sunday = false
        };

        Assert.True(settings.IsDayActive(DayOfWeek.Monday));
        Assert.True(settings.IsDayActive(DayOfWeek.Wednesday));
        Assert.False(settings.IsDayActive(DayOfWeek.Friday));
        Assert.False(settings.IsDayActive(DayOfWeek.Sunday));
    }
}
