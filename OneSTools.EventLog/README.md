# OneSTools.EventLog
[![Nuget](https://img.shields.io/nuget/v/OneSTools.EventLog)](https://www.nuget.org/packages/OneSTools.EventLog)<br>
Библиотека для чтения и парсинга данных журнала регистрации 1С

Предоставляет легкий интерфейс для чтения журнала регистрации. Позволяет работать в "live" режиме, либо считывать только доступные данные журнала.

Чтение журнала регистрации осуществляется с помощью класса **EventLogReader**. Конструктор класса принимает в качестве аргумента экземпляр класса **EventLogReaderSettings**, содержащий свойства:  
1. *LogFolder* - путь к каталогу журнала регистрации 1С.  
2. *LiveMode* - если свойство выставлено в true, то при достижении конца файла метод **ReadNextEventLogItem** не будет возвращать null, а будет ожидать появления в файле новой порции данных. Если false - то при достижении конца файла, вместо экземпляра класса **EventLogItem** будет возвращено null.
3. *ReadingTimeout* - Количество миллисекунд, сколько класс будет ожидать появления новых данных при достижении конца файла. Имеет эффект только если свойство **LiveMode** выставлено в true. По истечении указанного времени будет вызвано исключение **EventLogReaderTimeoutException**.  
4. *LgpFileName* - наименование **lgp** файла, с которого требуется начать чтение журнала.  
5. *LgpStartPosition* - позиция (в байтах) **lgp** файла, с которой требуется начать чтение журнала.  
6. *LgfStartPosition* - позиция (в байтах) **lgf** файла, с которой требуется начать чтение журнала.  
7. *ItemId* - номер, с которого будет начинаться нумерация считываемых событий (свойство **Id** класса **EventLogItem**).  
8. *TimeZone* - часовой пояс (в формате IANA Time Zone Database), в котором записан журнал регистрации. По умолчанию - часовой пояс системы.  

*Параметры 4-7 нужны, если предполагается использование библиотеки в качестве ядра службы, работа которой может быть приостановлена и возобновлена в любое время. Предполагается что служба может возобновить состояние ридера с последующим продолжением чтения данных с последней сохраненной (во внешней системе) позиции.*  

Пример использования библиотеки:  
```csharp
var eventLogReaderSettings = new EventLogReaderSettings
{
    LogFolder = "C:\\LogFolder",
    LiveMode = true,
    ReadingTimeout = 2 * 1000
};

var eventLogReader = new EventLogReader(settings);

try
{
  // or wait for a cancellation token's cancel request
  while (true)
  {
    var item = eventLogReader.ReadNextEventLogItem();

    if (item is null)
      break;
    else
    {
      // to do something with the event data
    }
  } 
}
catch (EventLogReaderTimeoutException)
{
  // timeout occurred
}
catch (Exception ex)
{
  // something went wrong
}
```
