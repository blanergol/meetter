# Meetter (.NET 9, WinForms + WPF)

Приложение для Windows, агрегирующее встречи из календарей (Google Calendar, далее — другие), извлекающее ссылки Google Meet/Zoom и показывает их в удобном списке.

## Проекты
- `Meetter.WinForms` — основное приложение (WinForms UI)
- `Meetter.App` — альтернативный WPF UI
- `Meetter.Core` — модели/интерфейсы/детекторы ссылок
- `Meetter.Persistence` — настройки и кеш
- `Meetter.Providers.Google` — провайдер Google Calendar (OAuth)

## Требования
- Windows 10/11
- .NET SDK 9.0 + Workload Windows Desktop:
```powershell
dotnet workload install windowsdesktop
```
- Собирайте и запускайте из обычного пути на диске Windows (например, `C:\dev\meetter`), а не из `\\wsl$`.

## Сборка и публикация (WinForms)
```powershell
# В папке решения
dotnet restore

# Публикация единым exe (win-x64)
dotnet publish Meetter.WinForms -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=true -p:DebugType=none `
  -o C:\Apps\Meetter

# Запуск
C:\Apps\Meetter\Meetter.WinForms.exe
```

Публикация WPF (при необходимости):
```powershell
dotnet publish Meetter.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=true -p:DebugType=none `
  -o C:\Apps\Meetter.Wpf
```

## Первый запуск
1. Создайте OAuth 2.0 Client ID (Desktop App) в Google Cloud и скачайте `credentials.json`.
2. Положите `credentials.json` рядом с exe. Если файла нет, приложение предложит выбрать его при добавлении аккаунта.
3. Откройте Настройки → вкладка «Аккаунты» → «Добавить Google аккаунт». Откроется браузер для авторизации.
4. Токен будет сохранён в `%APPDATA%/Meetter/google/<email>`.

## Настройки
Файл: `%APPDATA%/Meetter/settings.json`
```json
{
  "daysToShow": 3,
  "accounts": [
    {
      "providerId": "google",
      "email": "user@example.com",
      "displayName": "user@example.com",
      "enabled": true,
      "properties": {
        "credentialsPath": "credentials.json",
        "tokenPath": "%APPDATA%/Meetter/google/user@example.com"
      }
    }
  ]
}
```

## Кеш встреч
- Файловый кеш на 1 час: `%APPDATA%/Meetter/cache/meetings-YYYYMMDD-YYYYMMDD.json`
- Автозагрузка использует кеш; кнопка «Обновить» принудительно перезапрашивает и обновляет кеш.

## UI
- Главное окно: заголовок и «Обновить» в одной строке (кнопка справа), ниже список встреч.
- Список сгруппирован по датам: «Сегодня» для текущей, далее даты. Есть лоадер на время загрузки.
- Клик по встрече открывает ссылку в браузере/клиенте.

## Траблшутинг
- При ошибке «Calendar API disabled/Forbidden»: включите Google Calendar API для проекта, к которому относится ваш `credentials.json`, подождите 2–5 минут и повторите вход.
- Чтобы переавторизоваться, удалите папку токена `%APPDATA%/Meetter/google/<email>` и добавьте аккаунт заново.
- При ошибке «file in use» при публикации: закройте запущенный exe или публикуйте в новую папку (`-o C:\Apps\Meetter-<timestamp>`).

## Расширяемость
- Новые провайдеры календарей: реализуйте `ICalendarProvider` в отдельной библиотеке.
- Новые платформы встреч: реализуйте `IMeetingLinkDetector` (Regex-детекторы уже для Meet/Zoom, можно дополнять).
