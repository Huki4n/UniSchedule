# UniSchedule. Run `make` to list targets.
SHELL := cmd.exe
.SHELLFLAGS := /C

.PHONY: help build test test-unit test-e2e run build-debug test-debug test-debug-unit test-debug-e2e import data publish installer install uninstall

TFM := net10.0-windows10.0.17763.0
EXE := UniSchedule/bin/Release/$(TFM)/UniSchedule.exe
DIST := dist/UniSchedule

help:
	@echo build            Release build
	@echo test             Release tests, all categories
	@echo test-unit        Release tests without E2E
	@echo test-e2e         Release tests, category E2E
	@echo run              Run the Release app
	@echo build-debug      Debug build
	@echo test-debug       Debug tests, all categories
	@echo test-debug-unit  Debug tests without E2E
	@echo test-debug-e2e   Debug tests, category E2E
	@echo import           make import FILE=file.xlsx OUT=report.txt
	@echo data             make data DB=D:\temp\schedule.db
	@echo publish          Self-contained win-x64 into dist/UniSchedule
	@echo installer        Build dist/UniSchedule-Setup.exe
	@echo install          Copy dist into the user profile and add a Start menu shortcut
	@echo uninstall        Remove the installed copy and shortcut, keep the database

# Собрать приложение в Release.
build:
	dotnet build UniSchedule.slnx -c Release

# Прогнать тесты Release, все категории. Пишут во временную базу, не в профиль.
test:
	dotnet test UniSchedule.slnx -c Release --nologo

# Прогнать тесты Release без категории E2E.
test-unit:
	dotnet test UniSchedule.slnx -c Release --nologo --filter "Category!=E2E"

# Прогнать сквозные тесты Release, категория E2E.
test-e2e:
	dotnet test UniSchedule.slnx -c Release --nologo --filter "Category=E2E"

# Запустить приложение Release. База — в профиле пользователя.
run:
	dotnet run --project UniSchedule/UniSchedule.csproj -c Release

# Собрать Debug. Используйте, если запущенное приложение держит Release-exe.
build-debug:
	dotnet build UniSchedule.slnx -c Debug

# Прогнать тесты Debug, все категории, не перезаписывая занятый Release-exe.
test-debug:
	dotnet test UniSchedule.slnx -c Debug --nologo

# Прогнать тесты Debug без категории E2E.
test-debug-unit:
	dotnet test UniSchedule.slnx -c Debug --nologo --filter "Category!=E2E"

# Прогнать сквозные тесты Debug, категория E2E.
test-debug-e2e:
	dotnet test UniSchedule.slnx -c Debug --nologo --filter "Category=E2E"

# Импорт Excel в базу профиля уже собранным Release-exe.
# make import FILE=file.xlsx OUT=report.txt
import:
	@if "$(FILE)"=="" (echo make import FILE=file.xlsx OUT=report.txt & exit /b 1)
	@if "$(OUT)"=="" (echo make import FILE=file.xlsx OUT=report.txt & exit /b 1)
	@if not exist "$(EXE)" (echo Сначала выполните make build & exit /b 1)
	@"$(EXE)" --import "$(FILE)" --out "$(OUT)"

# Запустить приложение со своей базой. Ключ автозапуска Windows не меняется.
# make data DB=D:\temp\schedule.db
data:
	@if "$(DB)"=="" (echo make data DB=D:\temp\schedule.db & exit /b 1)
	dotnet run --project UniSchedule/UniSchedule.csproj -c Release -- --data "$(DB)"

# Опубликовать Release win-x64 со встроенным runtime в dist/UniSchedule.
publish:
	dotnet publish UniSchedule/UniSchedule.csproj -c Release -r win-x64 --self-contained true -o $(DIST)

# Собрать dist/UniSchedule-Setup.exe. Нужен Inno Setup 6.
installer: publish
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-installer.ps1

# Скопировать публикацию в %LocalAppData%\UniSchedule\app и создать ярлык «Расписание».
install:
	@if not exist "$(DIST)\UniSchedule.exe" (echo Сначала выполните make publish & exit /b 1)
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install.ps1 -Action install -Source "$(abspath $(DIST))"

# Удалить каталог app и ярлык. Базу schedule.db не трогает.
uninstall:
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install.ps1 -Action uninstall
