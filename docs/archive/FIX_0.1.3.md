# Исправление FireCalc v0.1.3 (21.10.2026) — дескриптор загрузки Renga

## Проверенный источник шаблона
Полученный от пользователя работающий модуль `RengaServerSwitcher.rar` содержит файл `RengaServerSwitcher.rndesc` с обязательными тегами `Name`, `Version`, `RequiredAPIVersion`, `Vendor`, `Copyright`, `PluginFilename`, `PluginType`.
В FireCalc v0.1.2 отсутствовал `Copyright`, из-за чего Renga 9.3 регистрировала ошибку `Required attribute 'Copyright' does not exist`.
До этого в v0.1.1 загрузка завершалась на ошибке формата `Version` (`0.1.0` — три части, Renga ожидала две или четыре).

## Исправления
- Добавлен обязательный `<Copyright>kartashov_ma, 2026</Copyright>`.
- Версия заменена на `<Version>0.1.3.0</Version>` (тот же формат из 4 частей, что и у Server Switcher).
- `<RequiredAPIVersion>2.50</RequiredAPIVersion>` соответствует версии, заявленной рабочим модулем и текущему SDK Renga 9.3.
- `Vendor` согласован с рабочим плагином (`kartashov_ma`).
- Имя библиотеки осталось `<PluginFilename>Renga.FireCalc.dll</PluginFilename>`, тип `Net8`.

Изменён **только дескриптор** (и документация). Файлы C# не изменены. DLL, успешно собранная пользователем для v0.1.1, остаётся применимой — не нужно собирать новую для этой правки.

## Быстрая проверка без сборки
1. Закрыть Renga полностью.
2. Заменить `C:\Program Files\Renga Professional\Plugins\FireCalc\FireCalc.rndesc` на файл `FireCalc.rndesc` из этого архива / предоставленный отдельным файлом.
3. Запустить Renga и открыть `Настройки → Подключаемые модули`.
4. Если модуль отсутствует, сразу сохранить и прислать новый `AecApp.log`; дескриптор может пройти проверку, но загрузка DLL и её зависимостей пока не проверены.

## Проверка нового исходного архива после пересборки
`FireCalc.Plugin.csproj` помечает файл `FireCalc.rndesc` как `CopyToOutputDirectory=PreserveNewest`: при пересборке он должен копироваться в `_build/FireCalc/` рядом с DLL. Для прямой замены пересборка не требуется.
