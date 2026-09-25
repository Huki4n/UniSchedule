# Документация UniSchedule

Поведение приложения по областям. Перед изменением функции читай соответствующий файл и обновляй его вместе с кодом.

Как собрать и запустить — в [README.md](../README.md). Контракты для правок кода — в [AGENTS.md](../AGENTS.md).

| Файл | Область |
| --- | --- |
| [startup.md](startup.md) | Запуск, аргументы, один экземпляр, композиция |
| [interaction.md](interaction.md) | Окна, поиск, карточки, трей, формы |
| [loading.md](loading.md) | Чтение доски и импорт Excel |
| [schedule.md](schedule.md) | Недели, карточки, строка «Ближайшая» |
| [notifications.md](notifications.md) | Напоминания и тосты |
| [storage.md](storage.md) | SQLite, ключи, перенос старой базы |

## Куда класть код

Один проект приложения `UniSchedule` и один тестовый `UniSchedule.Tests`.

| Папка | Ответственность | Может зависеть от |
| --- | --- | --- |
| `Models` | Данные и сохранённые коды | ничего |
| `Services` | Недели, разбор текста и Excel, напоминания, формы, ОС | `Models` |
| `Data` | SQLite | `Models` |
| `ViewModels` | Карточки и сборка доски | `Models`, `Services` |
| `Windows`, `MainWindow`, `App` | Окна, диалоги, привязки, композиция | всё остальное |
| `Converters` | Только отображение | ничего прикладного |

`App` создаёт `AppDatabase`, `NotificationService`, окно и трей. Новый экран: окно в `Windows`, правила — в `Services` и тест без WPF. Новое хранение — методы `AppDatabase`, SQL из окна не писать.
