using UniSchedule.Data;
using UniSchedule.Models;

namespace UniSchedule.Tests;

public sealed partial class AppDatabaseTests
{
    [Fact]
    public void Lessons_RoundTrip_DropsSecondsAndKeepsNullWeeks()
    {
        var lesson = new Lesson
        {
            GroupCode = "11-321",
            DayOfWeek = DayOfWeek.Wednesday,
            Start = new TimeSpan(8, 30, 45),
            End = new TimeSpan(10, 0, 15),
            Subject = "Алгоритмы",
            LessonType = LessonCodes.Practice,
            Teacher = "Иванов И.И.",
            Room = "1301",
            MeetingUrl = "https://telemost.yandex.ru/j/1",
            LmsUrl = "https://edu.kpfu.ru/course/1",
            Parity = WeekParity.Even,
            WeekFrom = 2,
            WeekTo = null,
            Notes = "перенос",
            RawText = "исходный текст",
            Source = LessonCodes.Manual,
        };

        var id = _database.UpsertLesson(lesson);
        var loaded = Assert.Single(_database.GetLessons("11-321"));

        Assert.Equal(id, loaded.Id);
        Assert.Equal(new TimeSpan(8, 30, 0), loaded.Start);
        Assert.Equal(new TimeSpan(10, 0, 0), loaded.End);
        Assert.Equal(LessonCodes.Practice, loaded.LessonType);
        Assert.Equal(WeekParity.Even, loaded.Parity);
        Assert.Equal(2, loaded.WeekFrom);
        Assert.Null(loaded.WeekTo);
        Assert.Equal("исходный текст", loaded.RawText);
        Assert.Equal(LessonCodes.Manual, loaded.Source);

        loaded.Subject = "Алгоритмы и структуры";
        _database.UpsertLesson(loaded);
        Assert.Equal(
            "Алгоритмы и структуры",
            Assert.Single(_database.GetLessons("11-321")).Subject
        );

        _database.DeleteLesson(id);
        Assert.Empty(_database.GetLessons("11-321"));
    }

    [Fact]
    public void ReplaceImported_RemovesOnlyImportedRows()
    {
        _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Ручная",
                Source = LessonCodes.Manual,
                DayOfWeek = DayOfWeek.Monday,
            }
        );

        var count = _database.ReplaceImported([
            new Lesson
            {
                GroupCode = "11-405",
                Subject = "Импорт",
                DayOfWeek = DayOfWeek.Tuesday,
                Source = LessonCodes.Manual,
            },
        ]);

        Assert.Equal(1, count);
        Assert.Equal(["11-321", "11-405"], _database.GetGroups());
        Assert.Equal(LessonCodes.Manual, Assert.Single(_database.GetLessons("11-321")).Source);
        Assert.Equal(LessonCodes.Imported, Assert.Single(_database.GetLessons("11-405")).Source);

        _database.ReplaceImported([], ["11-405"]);
        Assert.Single(_database.GetAllLessons());
        Assert.Equal("Ручная", _database.GetAllLessons()[0].Subject);
    }

    [Fact]
    public void ReplaceImported_KeepsLessonsOfOtherGroups()
    {
        _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-405",
                Subject = "Чужая",
                Source = LessonCodes.Imported,
                DayOfWeek = DayOfWeek.Monday,
            }
        );

        _database.ReplaceImported([
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Своя",
                DayOfWeek = DayOfWeek.Tuesday,
            },
        ]);

        Assert.Equal("Чужая", Assert.Single(_database.GetLessons("11-405")).Subject);
        Assert.Equal("Своя", Assert.Single(_database.GetLessons("11-321")).Subject);
        Assert.Equal(LessonCodes.Imported, _database.GetLessons("11-321")[0].Source);

        _database.ReplaceImported([]);
        Assert.Equal("Чужая", Assert.Single(_database.GetLessons("11-405")).Subject);
    }

    [Fact]
    public void Homework_RoundTrip_AndDeleteLessonRemovesIt()
    {
        var lessonId = _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Сети",
                DayOfWeek = DayOfWeek.Friday,
                Start = new TimeSpan(10, 10, 0),
            }
        );
        var id = _database.UpsertHomework(
            new Homework
            {
                LessonId = lessonId,
                Title = "  ЛР 1  ",
                Description = "  отчёт  ",
                Url = " https://edu.kpfu.ru/1 ",
                ExtraUrl = " https://disk.yandex.ru/1 ",
                Deadline = new DateTime(2026, 9, 15, 18, 30, 0),
                IsDone = true,
            }
        );

        var loaded = Assert.Single(_database.GetHomework("11-321"));
        Assert.Equal(id, loaded.Id);
        Assert.Equal(lessonId, loaded.LessonId);
        Assert.Equal("ЛР 1", loaded.Title);
        Assert.Equal("отчёт", loaded.Description);
        Assert.Equal("https://edu.kpfu.ru/1", loaded.Url);
        Assert.Equal("https://disk.yandex.ru/1", loaded.ExtraUrl);
        Assert.Equal(new DateTime(2026, 9, 15), loaded.Deadline);
        Assert.True(loaded.IsDone);
        Assert.Equal(1, _database.CountHomework(lessonId));

        loaded.Title = "ЛР 2";
        loaded.IsDone = false;
        _database.UpsertHomework(loaded);
        Assert.Equal("ЛР 2", Assert.Single(_database.GetHomework("11-321")).Title);
        Assert.False(_database.GetHomework("11-321")[0].IsDone);

        _database.DeleteLesson(lessonId);
        Assert.Empty(_database.GetHomework("11-321"));
        Assert.Equal(0, _database.CountHomework(lessonId));
    }

    [Fact]
    public void ElectivePick_SurvivesClear_AndBackupCopiesLessons()
    {
        _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Основы Linux",
                DayOfWeek = DayOfWeek.Tuesday,
                Start = new TimeSpan(12, 0, 0),
                ElectiveKey = "11-321|2|12:00",
            }
        );
        _database.SetElectivePick(
            "11-321",
            DayOfWeek.Tuesday,
            new TimeSpan(12, 0, 0),
            "Основы Linux"
        );
        _database.SetElectivePick(
            "11-321",
            DayOfWeek.Tuesday,
            new TimeSpan(12, 0, 0),
            "Кроссплатформенная разработка"
        );

        var pick = Assert.Single(_database.GetElectivePicks());
        Assert.Equal("Кроссплатформенная разработка", pick.Subject);
        Assert.Equal("12:00", pick.Start);
        Assert.Equal("11-321|2|12:00", Assert.Single(_database.GetLessons("11-321")).ElectiveKey);

        _database.ClearStoredData();
        Assert.Empty(_database.GetLessons("11-321"));
        Assert.Equal(
            "Кроссплатформенная разработка",
            Assert.Single(_database.GetElectivePicks()).Subject
        );

        _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Основы Linux",
                DayOfWeek = DayOfWeek.Tuesday,
                Start = new TimeSpan(12, 0, 0),
            }
        );
        var copyPath = Path.Combine(_directory, "copy.db");
        _database.BackupTo(copyPath);
        var copy = new AppDatabase(copyPath);
        Assert.Equal("Основы Linux", Assert.Single(copy.GetLessons("11-321")).Subject);
        Assert.Throws<InvalidOperationException>(() =>
            _database.BackupTo(Path.Combine(_directory, "schedule.db"))
        );

        _database.UpsertLesson(
            new Lesson
            {
                GroupCode = "11-321",
                Subject = "Новая",
                DayOfWeek = DayOfWeek.Wednesday,
                Start = new TimeSpan(14, 0, 0),
            }
        );
        _database.RestoreFrom(copyPath);
        Assert.Equal("Основы Linux", Assert.Single(_database.GetLessons("11-321")).Subject);
        Assert.Throws<InvalidOperationException>(() =>
            _database.RestoreFrom(Path.Combine(_directory, "schedule.db"))
        );
    }

    [Fact]
    public void ElectiveSubjects_ReplaceSet_AndEmptyIsStored()
    {
        Assert.Null(_database.GetElectiveSubjects("11-321"));

        _database.SetElectiveSubjects(
            "11-321",
            ["Основы Linux", " основы linux ", "Кроссплатформенная разработка"]
        );
        Assert.Equal(
            ["Кроссплатформенная разработка", "Основы Linux"],
            _database.GetElectiveSubjects("11-321")
        );

        _database.SetElectiveSubjects("11-321", []);
        Assert.Empty(_database.GetElectiveSubjects("11-321")!);

        _database.ClearStoredData();
        Assert.Empty(_database.GetElectiveSubjects("11-321")!);
    }
}
