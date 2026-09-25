using ClosedXML.Excel;
using UniSchedule.Models;
using UniSchedule.Services;

namespace UniSchedule.Tests;

public class ItisExcelParserTests
{
    [Fact]
    public void Parse_ReadsGroupsLessonsAndLinkRanges()
    {
        var path = Path.Combine(Path.GetTempPath(), $"unischedule-{Guid.NewGuid():N}.xlsx");
        try
        {
            WriteWorkbook(path);
            var result = ItisExcelParser.Parse(path);

            Assert.Equal(["11-321", "11-205"], result.Groups);
            Assert.Equal(2, result.Lessons.Count);
            Assert.Equal(2, result.LinkMatches);

            var databases = result.Lessons.Single(lesson => lesson.GroupCode == "11-321");
            Assert.Equal(DayOfWeek.Monday, databases.DayOfWeek);
            Assert.Equal(new TimeSpan(8, 30, 0), databases.Start);
            Assert.Equal("Базы данных", databases.Subject);
            Assert.Equal("https://telemost.yandex.ru/j/db", databases.MeetingUrl);
            Assert.Contains("edu.kpfu", databases.LmsUrl, StringComparison.OrdinalIgnoreCase);

            var networks = result.Lessons.Single(lesson => lesson.GroupCode == "11-205");
            Assert.Equal("https://zoom.us/j/net", networks.MeetingUrl);
            Assert.Contains("Группы 11-999 в файле нет", result.FormatStoredMessage(2, "11-999"), StringComparison.Ordinal);
            Assert.DoesNotContain("в файле нет", result.FormatStoredMessage(2, "11-321"), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void WriteWorkbook(string path)
    {
        using var workbook = new XLWorkbook();
        var schedule = workbook.AddWorksheet("Расписание");
        schedule.Cell(2, 3).Value = "11-321";
        schedule.Cell(2, 4).Value = "11-205";
        schedule.Cell(3, 1).Value = "Понедельник";
        schedule.Cell(3, 2).Value = "8:30-10:00";
        schedule.Cell(3, 3).Value = "Базы данных, лекция Иванов И.И. ауд. 1301";
        schedule.Cell(3, 4).Value = "Сети, практика Петров П.П. ауд. 1402";

        var links = workbook.AddWorksheet("Ссылки");
        links.Cell(2, 2).Value = "Базы данных";
        links.Cell(2, 3).Value = "Иванов И.И.";
        links.Cell(2, 4).Value = "11-321";
        links.Cell(2, 5).Value = "https://telemost.yandex.ru/j/db";
        links.Cell(2, 6).Value = "https://edu.kpfu.ru/course/1";
        links.Cell(3, 2).Value = "Сети";
        links.Cell(3, 3).Value = "Петров П.П.";
        links.Cell(3, 4).Value = "11-201-11-210";
        links.Cell(3, 5).Value = "https://zoom.us/j/net";
        workbook.SaveAs(path);
    }
}
