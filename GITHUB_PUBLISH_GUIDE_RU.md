# Публикация Renga FireCalc на GitHub

Этот пакет подготовлен по модели оформления проекта `Pilot-BCF-Exchange`: значки/badges, таблица функциональности, схема, инструкция, ограничения, changelog и отдельное описание релиза.

## 1. Новый репозиторий

**Рекомендуемое имя:** `Renga-FireCalc`.

**Description (можно скопировать):**

> Local hydraulic calculation and BIM diagnostics plugin for Renga 9.3 (.NET 8, C#, WPF). Pipe volumes, water/foam reserves, route/port analysis and Excel export.

**Topics:** `renga`, `renga-api`, `bim`, `fire-protection`, `hydraulics`, `csharp`, `wpf`, `dotnet8`, `engineering`, `excel-export`, `plugin`.

**Сайт / website:** оставить пустым, если отдельного сайта проекта нет.

**Visibility:** **Private** — до разрешения на публичное раскрытие клиентских сценариев ЭФЭР.

## 2. Что загрузить

Загрузите **содержимое папки `Renga-FireCalc`**, чтобы в корне GitHub лежали `README.md`, `FireCalc.sln`, `FireCalc.Core/`, `FireCalc.Plugin/`, `FireCalc.Tests/`, `docs/`, `LICENSE`, `CHANGELOG.md`, `.github/`.

### Не загружать

- Оригинальные файлы расчётов или модели ЭФЭР: `.xlsx`, `.pdf`, `.dwg`, `.rnp`, `.ifc`.
- Папку сторонних плагинов `Plugins.zip`, DLL «Умной воды», «Аэропро» и пр.
- Официальные SDK DLL/TLB, а также любые лицензии, ключи, токены, приватные конфиги.
- `bin/`, `obj/`, `_build/`, `.vs/`, `FireCalc_build_log.txt`.

Перед `git push` выполните `git status` и убедитесь, что не добавились бинарники и материалы заказчика.

## 3. Порядок через Git

```powershell
git init
git add .
git status
git commit -m "docs: publish FireCalc v0.5.1 source and documentation"
git branch -M main
git remote add origin <URL_НОВОГО_РЕПОЗИТОРИЯ>
git push -u origin main
```

Для загрузки через браузер: **Add file → Upload files**, перетащите содержимое папки в корень, сохраните commit. Убедитесь, что сохранились скрытые файлы `.gitignore`, `.gitattributes`, `.github`.

## 4. Release v0.5.1

1. Откройте GitHub → Releases → **Draft a new release**.
2. Создайте тег **`v0.5.1`**.
3. Заголовок: **`Renga FireCalc v0.5.1 — source release`**.
4. Описание скопируйте из [docs/RELEASE_0.5.1_RU.md](docs/RELEASE_0.5.1_RU.md).
5. Используйте автоматически предлагаемую GitHub ссылку **Source code (zip)**. **Не прикладывайте сторонние SDK DLL/TLB.**
6. **Не прикладывайте скомпилированный ZIP как проверенный релиз**, пока не выполнены тесты в Renga.
7. Отметьте релиз как **Pre-release** (прототип).

## 5. Критерии готовности к публичному репозиторию

- Получить разрешение на использование названия ЭФЭР и публикацию чисел/формул из клиентской методики или обезличить их в коде и документации.
- Проверить права на публикуемые скриншоты, файлы и чужие инструменты.
- Убедиться, что выбранная лицензия соответствует вашим намерениям. Сейчас — **All rights reserved**, без открытой лицензии.
- Исключить чувствительные данные из истории всех коммитов, не только HEAD.

**Рекомендация:** сначала создать приватный репозиторий и подключить базовые тесты `FireCalc.Core`.
