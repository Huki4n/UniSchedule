namespace UniSchedule.Tests;

public class AppLaunchTests
{
    [Fact]
    public void ReadDataPath_ReturnsFullPath_AndIsolatesMutex()
    {
        var path = AppLaunch.ReadDataPath(["--data", "schedule.db"]);
        Assert.NotNull(path);
        Assert.True(Path.IsPathRooted(path));
        Assert.Equal(@"Local\UniSchedule.SingleInstance", AppLaunch.MutexName(null));
        Assert.NotEqual(AppLaunch.MutexName(null), AppLaunch.MutexName(path));
        Assert.Equal(AppLaunch.MutexName(path), AppLaunch.MutexName(path));
    }
}
