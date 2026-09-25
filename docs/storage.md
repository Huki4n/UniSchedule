# Хранение

Весь SQL — в `Data/AppDatabase`. Окна вызывают его методы.

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

`LessonType` и `Source` — коды из `LessonCodes`: `lecture`, `practice`, `lab`, `manual`, `imported`. Значения в базе не переименовывать.

`UpsertLesson`: `Id > 0` обновляет строку, иначе вставляет и записывает новый id в объект. `DeleteLesson` удаляет по id и не чистит `NotificationLog`. `GetAllLessons` сортирует по группе, дню и началу.

Импорт описан в [loading.md](loading.md): удаляются только строки `Source='imported'`.

## Settings

Таблица пар `Key` / `Value`. Чтение незнакомых ключей игнорирует. Отсутствующий ключ даёт значение по умолчанию из `AppSettings`.

| Ключ | Формат | По умолчанию |
| --- | --- | --- |
| `SelectedGroup` | текст | `11-321` |
| `SemesterStart` | сначала `yyyy-MM-dd` в инвариантной культуре, затем `DateTime.TryParse` | `2026-09-01` |
| `FirstReminderMinutes` | целое | `60` |
| `SecondReminderMinutes` | целое | `15` |
| `NotificationsEnabled` | `1` / `0` (`true` тоже читается) | включено |
| `Autostart` | `1` / `0` | выключено |
| `MinimizeToTray` | `1` / `0` | включено |

`SaveSettings` пишет дату как `yyyy-MM-dd` и флаги как `1`/`0`. Запись идёт одной транзакцией. При старте настройки читаются и сразу сохраняются снова.

## NotificationLog

Первичный ключ `(LessonId, FireDate, OffsetMinutes)`. `FireDate` — `yyyy-MM-dd`. `WasNotificationSent` ищет строку. `MarkNotificationSent` делает `INSERT OR IGNORE`. Кто пишет отметку — в [notifications.md](notifications.md).
