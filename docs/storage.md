# Хранение

Весь SQL — в частичном классе `Data/AppDatabase`. Окна вызывают его методы. Схема и файл базы — `AppDatabase.cs`, настройки — `AppDatabase.Settings.cs`, пары и импорт — `AppDatabase.Lessons.cs`, откат названия — `AppDatabase.SubjectRollback.cs`, журнал напоминаний — `AppDatabase.Notifications.cs`. Домашки и комментарии — [homework.md](homework.md), код в `AppDatabase.Homework.cs`.

## Файл

База профиля: `%LocalAppData%\UniSchedule\schedule.db`. Каталог создаётся при открытии.

Конструктор без пути включает перенос. Если рядом с exe лежит `schedule.db`, он копируется поверх профиля вместе с `-wal` и `-shm`, затем эти файлы удаляются из каталога exe. Отсутствующий боковой файл у источника удаляет такой же файл у приёмника, чтобы старый журнал не прилип к новой копии.

`new AppDatabase(path)` перенос не делает. Так открывается `--data` и тестовая база.

Журнал: `PRAGMA journal_mode=WAL`.

## Lessons

| Колонка | Смысл |
| --- | --- |
| `Id` | Автоинкремент |
| `GroupCode` | Группа, индекс `IX_Lessons_Group` |
| `DayOfWeek` | `int` .NET: вс=0 … сб=6 |
| `Start`, `End` | Текст `hh:mm`. Секунды при записи отбрасываются |
| `Subject`, `LessonType`, `Teacher`, `Room` | Строки, по умолчанию пустые |
| `MeetingUrl`, `LmsUrl` | Ссылки |
| `Parity` | `0` все, `1` нечётная, `2` чётная |
| `WeekFrom`, `WeekTo` | Целые или NULL |
| `Notes`, `RawText` | Заметки и исходный текст ячейки |
| `Source` | `manual` или `imported` |

`LessonType` и `Source` — коды из `LessonCodes`: `lecture`, `practice`, `lab`, `credit`, `exam`, `manual`, `imported`. Значения в базе не переименовывать.

`UpsertLesson`: `Id > 0` обновляет строку, иначе вставляет и записывает новый id в объект. `DeleteLesson` удаляет пару, строку `SubjectRollback` и домашки этой пары с комментариями. `NotificationLog` не чистится. `GetAllLessons` сортирует по группе, дню и началу.

Импорт описан в [loading.md](loading.md): удаляются только строки `Source='imported'`. Домашки этих пар перепривязываются или удаляются, как описано в [homework.md](homework.md). После замены удаляются строки `SubjectRollback`, чей `LessonId` больше не существует.

`ClearStoredData` одной транзакцией удаляет `HomeworkComment`, `Homework`, `SubjectRollback`, `NotificationLog` и `Lessons`. `Settings` не меняется.

## SubjectRollback

| Колонка | Смысл |
| --- | --- |
| `LessonId` | Первичный ключ, одна запись на пару |
| `OriginalSubject` | Первое название в этот календарный день |
| `ChangedOn` | `yyyy-MM-dd` |

`RememberSubjectRollback` делает `INSERT … ON CONFLICT DO NOTHING`: вторая смена названия в тот же день не затирает первое. `GetSubjectRollback` и `RememberSubjectRollback` перед чтением удаляют строки с другой датой. `ForgetSubjectRollback` удаляет запись, когда сохранённое название совпало с записанным первоначальным.

## Settings

Таблица пар `Key` / `Value`. Чтение незнакомых ключей игнорирует. Отсутствующий ключ даёт значение по умолчанию из `AppSettings`.

| Ключ | Формат | По умолчанию |
| --- | --- | --- |
| `SelectedGroup` | текст | `11-321` |
| `SemesterStart` | сначала `yyyy-MM-dd` в инвариантной культуре, затем `DateTime.TryParse` | `2026-09-01` |
| `ReminderMinutes` | числа больше 0 через запятую, по убыванию, без повторов. Пустая строка — напоминаний нет | `60,15` |
| `NotificationsEnabled` | `1` / `0` | включено |
| `Autostart` | `1` / `0` | выключено |
| `MinimizeToTray` | `1` / `0` | включено |

Флаги `NotificationsEnabled`, `Autostart` и `MinimizeToTray` читают `1`, `true` и `True`. Другое значение уже существующего ключа выключает флаг. Нет ключа — берётся значение по умолчанию.

`SaveSettings` пишет дату как `yyyy-MM-dd`, напоминания как `ReminderMinutes` и флаги как `1`/`0`. Старые ключи `FirstReminderMinutes` и `SecondReminderMinutes` больше не записываются. Если `ReminderMinutes` в базе нет, список собирается из этих двух ключей: отсутствующий ключ даёт 60 и 15, ноль отбрасывается. Запись идёт одной транзакцией. При старте настройки читаются и сразу сохраняются снова.

## NotificationLog

Первичный ключ `(LessonId, FireDate, OffsetMinutes)`. `FireDate` — `yyyy-MM-dd`. `WasNotificationSent` ищет строку. `MarkNotificationSent` делает `INSERT OR IGNORE`. Кто пишет отметку — в [notifications.md](notifications.md).
