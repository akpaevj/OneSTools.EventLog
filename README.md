# OneSTools.EventLog
Библиотека для чтения и парсинга данных журнала регистрации 1С (в разработке)

Предоставляет легкий интерфейс для чтения журнала регистрации. Позволяет работать в "live" режиме, либо считывать только доступные данные журнала.

Пример использования:

```csharp
using var reader = new EventLogReader("C:\\Users\\akpaev.e.ENTERPRISE\\Desktop\\1Cv8Log", true);

while (true)
{
    var item = reader.ReadNextEventLogItem(_cancellationTokenSource.Token);

    if (item == null)
        break;
}
```
