# Инструменты для чтения и экспорта журнала регистрации 1С

Репозиторий содержит как библиотеки так и готовые инструменты для чтения и экспорта журнала регистрации 1С в различные СУБД.

## Состав:

[OneSTools.EventLog](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog) - Библиотека для чтения журнала регистрации (старый формат, LGF и LGP файлы). Позволяет выполнять как разовое чтение данных, так и запуск в "live" режиме</br>
[OneSTools.EventLog.Exporter.Core](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.Core) - Библиотека для экспорта журнала регистрации. Предоставляет легкие интерфейсы для реализации и является ядром инструментов для экспорта в различные СУБД</br>
[OneSTools.EventLog.Exporter.ClickHouse](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.ClickHouse) - Инструмент для экспорта журнала регистрации в [ClickHouse](https://clickhouse.tech/)</br>
[OneSTools.EventLog.Exporter.ElasticSearch](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.ElasticSearch) - Инструмент для экспорта журнала регистрации в [ElasticSearch](https://www.elastic.co/)</br>
[OneSTools.EventLog.Exporter.SqlServer](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.SqlServer) - Инструмент для экспорта журнала регистрации в [Microsoft SQL Server](https://www.microsoft.com/ru-ru/sql-server/sql-server-2019)</br>

### Описание конфигурации:

*Инструменты реализованные для конечных СУБД могут быть запущены как службы Windows/Linux*

Конфигурационный файл служб для всех СУБД имеет общие настройки:
```json
"Exporter": {
    "LogFolder": "..\\1Cv8Log",
    "Portion": 10000
  }
```
где:</br>
*LogFolder* - Путь к каталогу журнала регистрации.</br>
*Portion* - Количество событий, которое будет отправлено в СУБД за одну итерацию.</br>

*Примеры конфигурационных файлов и детальное описание можно найти, перейдя на страницы соответствующих инструментов*
