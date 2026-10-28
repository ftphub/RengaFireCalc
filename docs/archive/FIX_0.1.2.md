# Изменение v0.1.2 (21.10.2026)

Журнал запуска Renga 9.3: `PluginSystem: Attribute 'Version' ... has wrong version format`.

Причина: `FireCalc.rndesc` имел `<Version>0.1.0</Version>` (три части).
Документация в `93 RengaSDK/Samples` и `Docs/how-to-implement-a-plugin.html`: допускается `x.x` либо `x.x.x.x`.

Исправлено: `<Version>0.1</Version>`. Код C#, DLL и другие теги дескриптора без изменений.

Для уже собранной v0.1.1: закройте Renga и замените только
`C:\Program Files\Renga Professional\Plugins\FireCalc\FireCalc.rndesc`
готовым исправленным `FireCalc.rndesc` из этого архива; пересобирать DLL не надо.
После запуска Renga проверьте список подключаемых модулей и новые строки в `AecApp.log`.
Если возникнет следующая ошибка, пришлите свежий лог: исправление формата версии не доказывает совместимость последующих этапов загрузки.
