# UniSchedule

Настольное расписание ИТИС: WPF, SQLite в профиле пользователя, импорт Excel, напоминания Windows.

Перед изменением поведения прочитай файл области в `docs/` и сначала запиши туда новое поведение. Код и тесты меняй только после этого, чтобы они совпали с документом. Карта: `docs/README.md`.

| Область | Файл |
| --- | --- |
| Запуск, аргументы, один экземпляр | `docs/startup.md` |
| Окна, поиск, трей, формы пары и настроек | `docs/interaction.md` |
| Недели, карточки, «Ближайшая» | `docs/schedule.md` |
| Домашки: месяц, форма, комментарии, таблицы | `docs/homework.md` |
| Чтение доски и импорт Excel | `docs/loading.md` |
| Напоминания и тосты | `docs/notifications.md` |
| SQLite, ключи, перенос базы | `docs/storage.md` |

## Проверки

Из корня репозитория (`make` без цели печатает список):

```text
make build
make test
make test-unit
make test-e2e
make run
make build-debug
make test-debug
make test-debug-unit
make test-debug-e2e
make import FILE=file.xlsx OUT=report.txt
make data DB=D:\temp\schedule.db
make publish
make installer
make install
make uninstall
```

`import` пишет в базу профиля и вызывает уже собранный Release-exe. `data` открывает указанный файл и не меняет ключ автозапуска. `publish` кладёт self-contained `win-x64` в `dist/UniSchedule`. `installer` собирает из этого `dist/UniSchedule-Setup.exe` (Inno Setup 6). `install` копирует публикацию в `%LocalAppData%\UniSchedule\app` без мастера. `uninstall` удаляет каталог `app` и ярлык, базу не трогает. Если Release-exe занят запущенным приложением, собирайте и тестируйте через `build-debug` и `test-debug`.

`test` и `test-debug` гоняют все категории. `test-unit` и `test-debug-unit` пропускают `E2E`. `test-e2e` и `test-debug-e2e` запускают только её. Сквозные тесты пишут во временную базу, не в профиль. Импорт запускает `UniSchedule.exe`. Сценарий окна поднимает настоящие окна в том же процессе: ввод мыши из тестового процесса до окна не доходит, если на экране уже открыто другое приложение.

При восстановлении тестового проекта NuGet сообщает NU1904 для транзитивного `System.Drawing.Common` 4.7.0 (трей через WinForms). Пакет в этом рефакторинге не обновлялся.

## Изменения поведения

- Оба интервала напоминаний `0` больше не подменяются на 60 и 15 минут. Напоминаний нет, тестовое уведомление из настроек работает как раньше.
- Домашка напоминает за минуты до конца дня дедлайна. Можно задать и минуту, и календарный месяц. Пресеты, если ключа нет: 7, 5, 3 и 1 день, 12 и 4 часа. Пустой список — напоминаний о сдаче нет. Окно то же, что у пары: две минуты.
- `SemesterStart` читается сначала как `yyyy-MM-dd` в инвариантной культуре, затем прежним `DateTime.TryParse`.
- «Ближайшая» и подсветка сегодняшнего дня считаются от одного `DateTime.Now`. Колонки доски берутся из просматриваемой недели.
- База хранится в `%LocalAppData%\UniSchedule\schedule.db`. Старый файл рядом с exe переносится туда при следующем запуске.
- Аргумент `--data` открывает указанный файл и не меняет ключ автозапуска.

## Границы

Один проект приложения и один тестовый. Новые проекты, Mediator и интерфейсы «на вырост» не добавлять.

| Папка | Ответственность | Может зависеть от |
| --- | --- | --- |
| `Models` | Данные и сохранённые коды | ничего |
| `Services` | Правила недель, разбор текста и Excel, напоминания, проверка форм, ОС (трей, автозапуск, тосты) | `Models` |
| `Data` | SQLite | `Models` |
| `ViewModels` | Карточки, доска расписания и сетка домашек | `Models`, `Services` |
| `Windows`, `MainWindow`, `App` | Окна, диалоги, привязки, композиция | всё остальное |
| `Converters` | Только отображение | ничего прикладного |

`App` создаёт `AppDatabase`, `NotificationService`, окно и трей. Service Locator нет.

Новый экран: окно в `Windows`, правила — в `Services` и тест без WPF. Новая интеграция хранения — методы `AppDatabase`, не SQL из окна.

## Контракты, которые нельзя ломать молча

- Файл `%LocalAppData%\UniSchedule\schedule.db`. Если рядом с exe ещё лежит старый `schedule.db`, при запуске он копируется поверх профиля (вместе с `-wal`/`-shm`) и удаляется из каталога exe.
- Таблицы `Lessons`, `Settings`, `NotificationLog`, `HomeworkNotificationLog`, `Homework`, `HomeworkComment`, `SubjectRollback`. Ключи настроек: `SelectedGroup`, `SemesterStart` (`yyyy-MM-dd`), `ReminderMinutes` (числа через запятую; пусто — напоминаний нет), `HomeworkReminderMinutes` (`месяц` и минуты больше 0 через запятую; пусто — напоминаний о сдаче нет; нет ключа — `10080,7200,4320,1440,720,240`; `HomeworkReminderDays` не читается), `NotificationsEnabled`, `Autostart`, `MinimizeToTray` (`1`/`0`). Если `ReminderMinutes` нет, читаются `FirstReminderMinutes` и `SecondReminderMinutes`. `Homework.Deadline` — `yyyy-MM-dd`, `IsDone` — `1`/`0`, `Url` и `ExtraUrl` — текст. `HomeworkComment.CreatedAt` — `yyyy-MM-dd HH:mm`. `SubjectRollback` хранит первое название пары за календарный день: `LessonId`, `OriginalSubject`, `ChangedOn` (`yyyy-MM-dd`). `HomeworkNotificationLog`: `HomeworkId`, `FireDate` (`yyyy-MM-dd` начала окна), `OffsetMinutes` (`-1` — месяц). Старая колонка `OffsetDays` при старте сбрасывает эту таблицу.
- Коды в `LessonCodes`: `lecture`, `practice`, `lab`, `credit`, `exam`, `manual`, `imported`. Повторный импорт удаляет только `Source='imported'`.
- Время в БД — `hh:mm`, секунды отбрасываются.
- Имена элементов и привязки в XAML (`GroupBox`, `LessonSearchBox`, `DaysHost`, `IsDimmed`, `Accent`, …) — часть UI-контракта.
- Один экземпляр: mutex `Local\UniSchedule.SingleInstance`, событие `Local\UniSchedule.Show`.
- Закрытие окна при `MinimizeToTray` прячет окно, выход — только из трея.

## Поведение, на которое опирается UI

- Поиск фильтрует и карточки, и строку «Ближайшая». Пустой результат поиска показывает текст пустого расписания.
- Напоминание срабатывает в интервале `[начало − offset, начало − offset + 2 минуты]` и помечается отправленным только после успешного тоста. `0` минут отключает этот offset. Оба нуля — автонапоминаний нет. Домашка срабатывает в том же окне до конца дня дедлайна и помечается отправленной только после успешного тоста.
- Воскресенье в доске нет. Пара в воскресенье при создании переносится на понедельник.

## Где править частые задачи

- Текст карточки и «ближайшая пара»: `ViewModels/ScheduleComposer.cs`
- Домашки: `docs/homework.md`. Сетка — `ViewModels/HomeworkCalendar.cs`, форма — `Services/HomeworkForm.cs`
- Чётность и номер недели: `Services/AcademicCalendar.cs`
- Общее название пар: `Services/LessonSeries.cs`
- Ячейка Excel и текст пары: `Services/LessonTextParser.cs`, лист целиком — `Services/ItisExcelParser.cs`
- Кого уведомлять: `Services/NotificationPlanner.cs`. Показ тоста — `NotificationService`. Ошибка показа пишется в `%TEMP%\unischedule-toast.txt` и один раз за сессию открывает диалог; тестовая кнопка показывает диалог каждый раз. Неудачный показ не помечается отправленным.
- Стили: `Themes/Colors.xaml`, `Themes/Controls.xaml`, `Themes/Calendar.xaml`. Ключи (`AppBg`, `AccentButton`, `LabelText`, `DialogScrollViewer`) не переименовывать. Конвертеры остаются в `App.xaml`.
- Поля диалогов: `LessonForm`, `HomeworkForm`, `SettingsForm`. Code-behind только читает контролы и вызывает эти правила.
