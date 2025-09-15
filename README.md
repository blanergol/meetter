# Meetter (WPF, .NET 8)

Приложение для Windows, агрегирующее встречи из календарей (Google Calendar, в будущем другие), извлекающее ссылки на Google Meet/Zoom.

## Требования
- Windows 10/11
- .NET SDK 8.0 + Workload Windows Desktop: `dotnet workload install windowsdesktop`
- В WSL проект не собирается (нет WindowsDesktop SDK). Открывайте в Windows среде (Visual Studio 2022) или PowerShell.

## Сборка
```powershell
# Установите Windows Desktop workload
dotnet workload install windowsdesktop

# В папке решения
dotnet restore
dotnet build Meetter.sln
```

## Первый запуск
1. Создайте OAuth 2.0 Client ID в Google Cloud (Desktop App) и скачайте `credentials.json`.
2. Положите файл рядом с exe или укажите путь в настройках провайдера Google.
3. При первом запросе к календарю откроется браузер для авторизации, токен сохранится в `.meetter/google-token`.

## Настройки
Файл настроек по умолчанию: `%APPDATA%/Meetter/settings.json`
```json
{
  "daysToShow": 3,
  "providers": [
    {
      "id": "google",
      "enabled": true,
      "properties": {
        "credentialsPath": "credentials.json",
        "tokenPath": ".meetter/google-token"
      }
    }
  ]
}
```

## Функции
- Главное окно: список встреч за 1..7 дней, сортировка по времени, кнопка подключения по наведению.
- Настройки: включение/удаление провайдеров, выбор дней, параметры провайдеров.
- О программе: сведения о версии и разработчике.

## Расширяемость
- Добавляйте новых провайдеров, реализуя `ICalendarProvider` в отдельной библиотеке.
- Для новых платформ встреч добавляйте реализации `IMeetingLinkDetector`.
