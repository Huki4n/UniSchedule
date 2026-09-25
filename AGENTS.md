# UniSchedule

Настольное расписание ИТИС: WPF, SQLite в профиле пользователя, импорт Excel, напоминания Windows.

## Проверки

Из корня репозитория (`make` без цели печатает список):

```text
make build
make test
make run
make build-debug
make test-debug
make import FILE=file.xlsx OUT=report.txt
make data DB=D:\temp\schedule.db
```

`import` пишет в базу профиля и вызывает уже собранный Release-exe. `data` открывает указанный файл и не меняет ключ автозапуска. Если Release-exe занят запущенным приложением, собирайте и тестируйте через `build-debug` и `test-debug`.

Сквозные тесты входят в `dotnet test` (категория `E2E`) и пишут во временную базу, не в профиль. Импорт запускает `UniSchedule.exe`. Сценарий окна поднимает настоящие окна в том же процессе: ввод мыши из тестового процесса до окна не доходит, если на экране уже открыто другое приложение.

При восстановлении тестового проекта NuGet сообщает NU1904 для транзитивного `System.Drawing.Common` 4.7.0 (трей через WinForms). Пакет в этом рефакторинге не обновлялся.

## Изменения поведения

- Оба интервала напоминаний `0` больше не подменяются на 60 и 15 минут. Напоминаний нет, тестовое уведомление из настроек работает как раньше.
- `SemesterStart` читается сначала как `yyyy-MM-dd` в инвариантной культуре, затем прежним `DateTime.TryParse`.
- Доска и строка «Ближайшая» считаются от одного `DateTime.Now`.
- База хранится в `%LocalAppData%\UniSchedule\schedule.db`. Старый файл рядом с exe переносится туда при следующем запуске.
- Аргумент `--data` открывает указанный файл и не меняет ключ автозапуска.

## Границы

Один проект приложения и один тестовый. Новые проекты, Mediator и интерфейсы «на вырост» не добавлять.

| Папка | Ответственность | Может зависеть от |
| --- | --- | --- |
| `Models` | Данные и сохранённые коды | ничего |
| `Services` | Правила недель, разбор текста и Excel, напоминания, проверка форм, ОС (трей, автозапуск, тосты) | `Models` |
| `Data` | SQLite | `Models` |
| `ViewModels` | Карточки и сборка доски расписания | `Models`, `Services` |
| `Windows`, `MainWindow`, `App` | Окна, диалоги, привязки, композиция | всё остальное |
| `Converters` | Только отображение | ничего прикладного |

`App` создаёт `AppDatabase`, `NotificationService`, окно и трей. Service Locator нет.

Новый экран: окно в `Windows`, правила — в `Services` и тест без WPF. Новая интеграция хранения — методы `AppDatabase`, не SQL из окна.

## Контракты, которые нельзя ломать молча

- Файл `%LocalAppData%\UniSchedule\schedule.db`. Если рядом с exe ещё лежит старый `schedule.db`, при запуске он копируется поверх профиля (вместе с `-wal`/`-shm`) и удаляется из каталога exe.
- Таблицы `Lessons`, `Settings`, `NotificationLog`. Ключи настроек: `SelectedGroup`, `SemesterStart` (`yyyy-MM-dd`), `FirstReminderMinutes`, `SecondReminderMinutes`, `NotificationsEnabled`, `Autostart`, `MinimizeToTray` (`1`/`0`).
- Коды в `LessonCodes`: `lecture`, `practice`, `lab`, `manual`, `imported`. Повторный импорт удаляет только `Source='imported'`.
- Время в БД — `hh:mm`, секунды отбрасываются.
- Имена элементов и привязки в XAML (`GroupBox`, `LessonSearchBox`, `DaysHost`, `IsDimmed`, `Accent`, …) — часть UI-контракта.
- Один экземпляр: mutex `Local\UniSchedule.SingleInstance`, событие `Local\UniSchedule.Show`.
- Закрытие окна при `MinimizeToTray` прячет окно, выход — только из трея.

## Поведение, на которое опирается UI

- Поиск фильтрует и карточки, и строку «Ближайшая». Пустой результат поиска показывает текст пустого расписания.
- Напоминание срабатывает в интервале `[начало − offset, начало − offset + 2 минуты]` и помечается отправленным только после успешного тоста. `0` минут отключает этот offset. Оба нуля — автонапоминаний нет.
- Воскресенье в доске нет. Пара в воскресенье при создании переносится на понедельник.

## Где править частые задачи

- Текст карточки и «ближайшая пара»: `ViewModels/ScheduleComposer.cs`
- Чётность и номер недели: `Services/AcademicCalendar.cs`
- Ячейка Excel и текст пары: `Services/LessonTextParser.cs`, лист целиком — `Services/ItisExcelParser.cs`
- Кого уведомлять: `Services/NotificationPlanner.cs`. Показ тоста — `NotificationService`. Ошибка показа пишется в `%TEMP%\unischedule-toast.txt` и один раз за сессию открывает диалог; тестовая кнопка показывает диалог каждый раз. Неудачный показ не помечается отправленным.
- Стили: `Themes/Colors.xaml`, `Themes/Controls.xaml`, `Themes/Calendar.xaml`. Ключи (`AppBg`, `AccentButton`, `LabelText`, `DialogScrollViewer`) не переименовывать. Конвертеры остаются в `App.xaml`.
- Поля диалогов: `LessonForm`, `SettingsForm`. Code-behind только читает контролы и вызывает эти правила.
