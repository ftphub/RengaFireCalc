# Установка и сборка FireCalc v0.5.1

## 1. Требования

- Windows x64, Renga Professional 9.3 (API 2.50).
- Для локальной сборки: Visual Studio 2022 или 2026, workload **.NET desktop development**, MSBuild, .NET 8 SDK и targeting pack.
- Официальный Renga SDK 9.3 с `RengaCOMAPI.tlb` и `Renga.NET8.PluginUtility.dll`.
- Для запуска плагина должна быть доступна среда выполнения .NET 8 Desktop Runtime соответствующей архитектуры.

## 2. Подключение официального SDK

Скачайте или получите официальный SDK легальным способом; этот репозиторий его не распространяет.

Из официального ZIP:

```powershell
.\PREPARE_SDK.ps1 -SdkZip "C:\Downloads\RengaSDK93.zip"
.\CHECK_PREREQUISITES.ps1
```

Или скопируйте вручную:

```text
sdk/RengaCOMAPI.tlb
sdk/Renga.NET8.PluginUtility.dll
```

Важно: папка `sdk/` должна оставаться локальной — `.gitignore` запрещает коммитировать DLL и TLB.

## 3. Сборка через Visual Studio

1. Откройте `FireCalc.sln`.
2. Выберите `Release`, `x64`.
3. Меню **Сборка → Пересобрать решение**.
4. Проверьте `_build/FireCalc/Renga.FireCalc.dll` и соседние файлы, в том числе `FireCalc.Core.dll`, `Renga.NET8.PluginUtility.dll`, `FireCalc.rndesc`.
5. Запустите `FireCalc.Tests` отдельно — успешная компиляция тестового проекта не означает их выполнения.

Консольная проверка независимого ядра:

```powershell
dotnet run --project .\FireCalc.Tests\FireCalc.Tests.csproj -c Release
```

## 4. Автоматическая сборка

Откройте **Developer PowerShell for Visual Studio** и выполните:

```powershell
.\BUILD_AND_DEPLOY.ps1
```

Это запустит тесты ядра, пересоберёт Release/x64, подготовит архив собранного модуля и сохранит лог сборки. Нельзя считать пакет испытанным до реального запуска в Renga.

## 5. Установка в Renga

1. Закройте Renga.
2. Сохраните прежнюю папку `FireCalc` за пределами `Plugins` (например, `Documents/FireCalcBackups`).
3. Полностью замените содержимое `C:\Program Files\Renga Professional\Plugins\FireCalc\` файлами `_build\FireCalc\`.
4. Запустите Renga и убедитесь, что модуль появился в «Настройки → Подключаемые модули».
5. Откройте тестовую **копию** проекта и выполните проверки [TEST_PLAN_RU.md](TEST_PLAN_RU.md).

Для автоматической установки после проверки сборки, если есть необходимые права:

```powershell
.\BUILD_AND_DEPLOY.ps1 -Install
```

Скрипт сохраняет предыдущее содержимое отдельно в `Documents/FireCalcBackups`.

## 6. Откат

Закройте Renga, удалите файлы ошибочной установки и скопируйте из резервной папки предыдущий рабочий комплект. Не храните резервные `.rndesc` внутри `Plugins`: Renga может попытаться подключить обе версии.
