using ClosedXML.Excel;
using UniSchedule.Data;
using UniSchedule.Models;

namespace UniSchedule.Tests.E2E;

[CollectionDefinition("E2E", DisableParallelization = true)]
public sealed class E2ECollection;

[Collection("E2E")]
public sealed class ScheduleFlowTests
{
    [Fact(Timeout = 120000)]
    [Trait("Category", "E2E")]
    public async Task User_AddsSearchesAndDeletesLesson_ThenTurnsTrayOff()
    {
        await Task.Yield();
        var directory = NewDirectory();
        var databasePath = Path.Combine(directory, "schedule.db");
        using var session = AppHarness.Start(databasePath);
        try
        {
            Assert.Contains("Неделя", session.Text("WeekLabel"), StringComparison.Ordinal);
            Assert.Contains("Пар нет", session.Text("NextLesson"), StringComparison.Ordinal);

            session.Press("Settings");
            session.WaitFor("SettingsWindow");
            session.SetText("FirstReminder", "0");
            session.SetText("SecondReminder", "0");
            session.Uncheck("MinimizeToTray");
            session.Press("SaveSettings");
            session.WaitGone("SettingsWindow");

            var saved = Read(databasePath);
            Assert.Equal(0, saved.FirstReminderMinutes);
            Assert.Equal(0, saved.SecondReminderMinutes);
            Assert.False(saved.MinimizeToTray);

            session.Press("AddLesson");
            session.WaitFor("LessonEditor");
            session.Press("SaveLesson");
            session.WaitFor("AppDialog");
            Assert.Contains("Нельзя сохранить", session.Text("DialogTitle"), StringComparison.Ordinal);
            session.Press("DialogOk");
            session.WaitGone("AppDialog");
            session.Press("CancelLesson");
            session.WaitGone("LessonEditor");

            var slot = UpcomingSlot();
            session.Press("AddLesson");
            session.WaitFor("LessonEditor");
            session.SelectIndex("LessonDay", slot.DayIndex);
            session.SetText("LessonSubject", "Э2Е Алгоритмы");
            session.SetText("LessonStart", slot.Start);
            session.SetText("LessonEnd", slot.End);
            session.Press("SaveLesson");
            session.WaitGone("LessonEditor");

            WaitText(session, "NextLesson", "Э2Е Алгоритмы");
            Assert.Equal("Э2Е Алгоритмы", ReadLessons(databasePath).Single().Subject);

            session.SetText("LessonSearch", "нет-такого");
            WaitText(session, "NextLesson", "Пар нет");
            session.SetText("LessonSearch", "");
            WaitText(session, "NextLesson", "Э2Е Алгоритмы");

            session.OpenLesson("Э2Е Алгоритмы");
            session.WaitFor("LessonEditor");
            session.Press("DeleteLesson");
            session.WaitFor("AppDialog");
            session.Press("DialogOk");
            session.WaitGone("LessonEditor");
            WaitText(session, "NextLesson", "Пар нет");
            Assert.Empty(ReadLessons(databasePath));

            var workbookPath = Path.Combine(directory, "schedule.xlsx");
            WriteWorkbook(workbookPath, secondGroup: true);
            MainWindow.ImportFileOverride = workbookPath;
            session.Press("Import");
            session.WaitFor("AppDialog");
            Assert.Equal("Импорт", session.Text("DialogTitle"));
            session.Press("DialogOk");
            session.WaitGone("AppDialog");
            WaitText(session, "NextLesson", "Базы данных");

            session.SelectItem("GroupBox", "11-408");
            WaitText(session, "NextLesson", "Сети");
            Assert.Equal("11-408", Read(databasePath).SelectedGroup);
            Assert.Equal("Сети", ReadLessons(databasePath, "11-408").Single().Subject);

            session.SelectItem("GroupBox", AppSettings.DefaultGroupCode);
            WaitText(session, "NextLesson", "Базы данных");
            session.OpenLesson("Базы данных");
            session.WaitFor("LessonEditor");
            session.SetText("LessonSubject", "Базы данных 2");
            session.Press("SaveLesson");
            session.WaitGone("LessonEditor");
            WaitText(session, "NextLesson", "Базы данных 2");
            Assert.Equal("Базы данных 2", ReadLessons(databasePath).Single().Subject);
            Assert.Equal("Сети", ReadLessons(databasePath, "11-408").Single().Subject);

            var start = new ProcessStartInfo(AppHarness.ExePath())
            {
                UseShellExecute = false
            };
            start.ArgumentList.Add("--data");
            start.ArgumentList.Add(databasePath);
            using var second = Process.Start(start) ?? throw new InvalidOperationException("Второй экземпляр не запустился.");
            Assert.True(second.WaitForExit(15000));
            Assert.Equal(0, second.ExitCode);
            Assert.Contains("Неделя", session.Text("WeekLabel"), StringComparison.Ordinal);

            session.CloseMain();
            Assert.True(session.Exited(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            MainWindow.ImportFileOverride = null;
            TryDelete(directory);
        }
    }

    [Fact(Timeout = 60000)]
    [Trait("Category", "E2E")]
    public async Task ImportCommand_WritesLessonsIntoDataFile()
    {
        await Task.Yield();
        var directory = NewDirectory();
        var databasePath = Path.Combine(directory, "schedule.db");
        var workbookPath = Path.Combine(directory, "schedule.xlsx");
        var reportPath = Path.Combine(directory, "report.txt");
        WriteWorkbook(workbookPath);

        using var process = ProcessStart(workbookPath, reportPath, databasePath);
        Assert.True(process.WaitForExit(20000));
        Assert.Equal(0, process.ExitCode);
        Assert.StartsWith("OK", File.ReadAllText(reportPath), StringComparison.Ordinal);
        Assert.Equal("Базы данных", ReadLessons(databasePath).Single().Subject);
        TryDelete(directory);
    }

    private static Process ProcessStart(string workbookPath, string reportPath, string databasePath)
    {
        var start = new ProcessStartInfo(AppHarness.ExePath())
        {
            UseShellExecute = false
        };
        start.ArgumentList.Add("--import");
        start.ArgumentList.Add(workbookPath);
        start.ArgumentList.Add("--out");
        start.ArgumentList.Add(reportPath);
        start.ArgumentList.Add("--data");
        start.ArgumentList.Add(databasePath);
        return Process.Start(start) ?? throw new InvalidOperationException("Импорт не запустился.");
    }

    private static (int DayIndex, string Start, string End) UpcomingSlot()
    {
        var now = DateTime.Now;
        var later = now.TimeOfDay + TimeSpan.FromHours(2);
        if (now.DayOfWeek != DayOfWeek.Sunday && later < new TimeSpan(22, 0, 0))
        {
            var start = new TimeSpan(later.Hours, 0, 0);
            return ((int)now.DayOfWeek - 1, start.ToString(@"hh\:mm"), start.Add(TimeSpan.FromHours(1)).ToString(@"hh\:mm"));
        }

        var date = now.Date.AddDays(1);
        if (date.DayOfWeek == DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return ((int)date.DayOfWeek - 1, "08:30", "10:00");
    }

    private static void WriteWorkbook(string path, bool secondGroup = false)
    {
        using var workbook = new XLWorkbook();
        var schedule = workbook.AddWorksheet("Расписание");
        schedule.Cell(2, 3).Value = "11-321";
        schedule.Cell(3, 1).Value = "Понедельник";
        schedule.Cell(3, 2).Value = "8:30-10:00";
        schedule.Cell(3, 3).Value = "Базы данных, лекция Иванов И.И. ауд. 1301";
        if (secondGroup)
        {
            schedule.Cell(2, 4).Value = "11-408";
            schedule.Cell(3, 4).Value = "Сети, лекция Петров П.П. ауд. 1402";
        }

        workbook.SaveAs(path);
    }

    private static void WaitText(AppHarness session, string automationId, string fragment)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < until)
        {
            if (session.Text(automationId).Contains(fragment, StringComparison.Ordinal))
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Текст «{fragment}» не появился в {automationId}. Сейчас: {session.Text(automationId)}");
    }

    private static AppSettings Read(string databasePath)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            try
            {
                return new AppDatabase(databasePath).GetSettings();
            }
            catch (Microsoft.Data.Sqlite.SqliteException) when (DateTime.UtcNow < until)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static List<Lesson> ReadLessons(string databasePath, string? group = null)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            try
            {
                return new AppDatabase(databasePath).GetLessons(group ?? AppSettings.DefaultGroupCode);
            }
            catch (Microsoft.Data.Sqlite.SqliteException) when (DateTime.UtcNow < until)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "unischedule-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string directory)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

}
