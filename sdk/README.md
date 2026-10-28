# Локальный SDK Renga — не включается в GitHub

Для сборки `FireCalc.Plugin` требуются файлы из официального Renga SDK 9.3 (API 2.50):

```text
sdk/
├── RengaCOMAPI.tlb
└── Renga.NET8.PluginUtility.dll
```

В репозиторий эти бинарные файлы не входят. Используйте официальный SDK, полученный на законных основаниях от правообладателя.

**Автоматическое копирование** из официального ZIP SDK:

```powershell
.\PREPARE_SDK.ps1 -SdkZip "C:\Path\To\RengaSDK93.zip"
```

Или вручную скопируйте `tlb/RengaCOMAPI.tlb` и `Net/Renga.NET8.PluginUtility.dll` в локальную папку `sdk`.

Проверяйте условия лицензии и распространения официального SDK перед публикацией любых связанных файлов.
