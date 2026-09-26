# UniSchedule. Run `make` to list targets.
SHELL := cmd.exe
.SHELLFLAGS := /C

.PHONY: help build test test-unit test-e2e run build-debug test-debug test-debug-unit test-debug-e2e import data

TFM := net10.0-windows10.0.17763.0
EXE := UniSchedule/bin/Release/$(TFM)/UniSchedule.exe

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
