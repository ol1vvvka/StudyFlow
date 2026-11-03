# Публикация Android (APK) и версионирование

## Версии

В `StudyFlow/StudyFlow.csproj`:

| Свойство | Смысл | Пример |
| --- | --- | --- |
| `ApplicationDisplayVersion` | Версия для пользователя (semver `X.Y.Z`) | `1.0.0` |
| `ApplicationVersion` | Номер сборки / Android `versionCode` | `10000` |

Код сборки кодируется так: **Major×10000 + Minor×100 + Patch**.  
`1.0.0` → `10000`, `1.0.1` → `10001`, `1.1.0` → `10100`, `2.0.0` → `20000`.

Перед каждой новой установкой на устройство или выкладкой:

1. Поднимите `ApplicationDisplayVersion` по смыслу изменений.
2. Выставьте `ApplicationVersion` по формуле выше (или просто больше предыдущего числа — Android требует монотонный рост `versionCode`).
3. Кратко опишите изменения в `CHANGELOG.md`. Релизный раздел с номером версии добавляйте только когда пакет реально выпущен.

В настройках приложения показывается строка вида `StudyFlow, версия 1.0.0 (10000)`.

## Формат пакета

Release для Android собирает **только APK** (`AndroidPackageFormat` / `AndroidPackageFormats` = `apk`).  
AAB нужен для Google Play — при публикации в магазин формат можно сменить отдельно.

## Подпись

1. Создайте keystore (команда в `Directory.Build.props.example`).
2. Положите файл в игнорируемый `secrets/` (не коммитьте).
3. Скопируйте `Directory.Build.props.example` → `Directory.Build.props` и поправьте путь/alias при необходимости.
4. Задайте пароль: `$env:STUDYFLOW_KEYSTORE_PASS = '…'` в сессии PowerShell перед публикацией.

Без `Directory.Build.props` Release APK соберётся, но не будет подписан вашим ключом (для раздачи «как есть» подпись обязательна).

## Сборка APK

Из корня репозитория:

```powershell
.\scripts\publish-android.ps1
```

Или вручную:

```powershell
dotnet publish StudyFlow/StudyFlow.csproj -f net10.0-android -c Release
```

Готовый файл копируется в `artifacts/publish/StudyFlow-<версия>-<код>.apk` (папка в `.gitignore`).  
Исходный APK также лежит в `StudyFlow/bin/Release/net10.0-android/publish/`.

Установка на устройство:

```powershell
adb install -r artifacts\publish\StudyFlow-1.0.0-10000.apk
```
