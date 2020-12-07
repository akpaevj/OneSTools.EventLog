# Инструменты для чтения и экспорта журнала регистрации 1С
Репозиторий содержит как библиотеки так и готовые инструменты для чтения и экспорта журнала регистрации 1С в ClickHouse и ElasticSearch.

## Roadmap:
1. Реализовать менеджер экспортёров с автоматическим подключением к экспорту логов новых баз и удалением обработанных файлов

## Состав:
|Наименование|Описание|Actions/Nuget|
|:-----------|:-------|:------------|
|[OneSTools.EventLog](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog)|Библиотека для чтения журнала регистрации (старый формат, LGF и LGP файлы). Позволяет выполнять как разовое чтение данных, так и запуск в "live" режиме||
|[OneSTools.EventLog.Exporter.Core](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.Core)|Библиотека-ядро для инструментов экспорта журнала регистрации, на основе которой можно создавать приложения для экспорта в новые СУБД||
|[OneSTools.EventLog.Exporter.ClickHouse](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.ClickHouse)|Базовый пакет, реализующий интерфейс IEventLogStorage для экспорта журнала регистрации 1С в [ClickHouse](https://clickhouse.tech/)||
|[OneSTools.EventLog.Exporter.ElasticSearch](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.ElasticSearch)|Базовый пакет, реализующий интерфейс IEventLogStorage для экспорта журнала регистрации 1С в [ElasticSearch](https://www.elastic.co/)||
|[EventLogExporter](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter)|Служба для экспорта журнала регистрации в [ClickHouse](https://clickhouse.tech/) и [ElasticSearch](https://www.elastic.co/)|![.NET 5](https://github.com/akpaevj/OneSTools.EventLog/workflows/.NET%205/badge.svg)|

## Get started:

### Конфигурация:
Файл конфигурации (appsettings.json) разбит на несколько секций, каждая из которых отвечают за функциональность определенной части приложения.

**Exporter:**
В этой секции размещены общие параметры экспортера, не зависящие от СУБД.
```json
"Exporter": {
    "StorageType": 2,
    "LogFolder": "C:\\Users\\akpaev.e.ENTERPRISE\\Desktop\\1Cv8Log",
    "Portion": 10000,
    "TimeZone": "Europe/Moscow",
    "WritingMaxDegreeOfParallelism": 8,
    "CollectedFactor": 8,
    "ReadingTimeout": 1,
    "LoadArchive": false
  }
```
где:</br>
1. *StorageType* - тип хранилища журнала регистрации. Может принимать значения:</br>
*1* - Clickhouse</br>
*2* - ElasticSearch</br>
2. *LogFolder* - путь к каталогу журнала регистрации 1С.</br>
3. *Portion* - Размер порции, записываемый в БД за одну итерацию (10000 по умолчанию)</br>
4. *TimeZone* - часовой пояс (в формате IANA Time Zone Database), в котором записан журнал регистрации. По умолчанию - часовой пояс системы</br>
5. *WritingMaxDegreeOfParallelism* - количество потоков записи в СУБД. Т.к. в ClickHouse не поддерживаются одновременные BULK операции, то параметр имеет смысл только для ElasticSearch. По умолчанию - 1.</br>
6. *CollectedFactor* - коэффициент количества элементов, которые могут быть помещены в очередь записи. Предельное количество элементов равно Portion * CollectedFactor. По умолчанию - 2.
7. *ReadingTimeout* - таймаут сброса данных при достижении конца файла (в секундах). По умолчанию - 1 сек.
8. *LoadArchive* - Специальный параметр, предназначенный для первоначальной загрузки архивных данных. При установке параметра в true, отключается "live" режим и не выполняется запрос последнего обработанного файла из БД</br>

**ClickHouse:**
Заполнение секции требуется, если в секции *Exporter* у параметра *StorageType* указано значение 1.
```json
"ConnectionStrings": {
    "Default": "Host=localhost;Port=8123;Username=default;password=;Database=database_name;"
  }
```
**ElasticSearch:**
```json
"ElasticSearch": {
    "Nodes": [
      {
        "Host": "http://192.168.0.95:9200",
        "AuthenticationType": "0"
      },
      {
        "Host": "http://192.168.0.93:9200",
        "AuthenticationType": "1",
        "UserName": "",
        "Password": ""
      }
      {
        "Host": "http://192.168.0.94:9200",
        "AuthenticationType": "2",
        "Id": "",
        "ApiKey": ""
      }
    ],
    "Index": "upp-main-el",
    "Separation": "M",
    "MaximumRetries": 2,
    "MaxRetryTimeout": 30
  }
```
где:</br>
1. *Nodes* - узел, содержащий хосты кластера ElasticSearch, либо один узел при работе с одной нодой. При недоступности текущего узла будет происходить переключение на следующий узел списка. Для узлов доступны 3 типа аутентификации: </br>
*0* - без аутентификации</br>
*1* - Basic</br>
*2* - ApiKey</br>
2. *Index* - префикс названия индекса, конечное название будет определено в зависимости от значения параметра Separation.</br>
3. *Separation* - метод разделения данных по индексам. Может принимать значения:</br>
*H* (Hour) - делить индексы по часам. Пример конечного названия индекса: index-name-el-2020010113</br>
*D* (Day) - делить индексы по дням. Пример конечного названия индекса: index-name-el-20200101</br>
*M* (Month) - делить индексы по месяцам. Пример конечного названия индекса: index-name-el-202001</br>
При указании любого другого (либо не указании вовсе) значения, разделения индекса не будет и конечное название индекса будет выглядеть так: index-name-el-all</br>
4. *MaximumRetries* - количество попыток переподключения к очередному узлу
5. *MaxRetryTimeout* - таймаут попытки подключения

Так-же при первом подключении к узлу приложение проверяет наличие шаблона индекса (Index template) с именем "oneslogs" и при отсутствии - создает. Если шаблон уже создан, то его перезапись происходить не будет, так как предполагается возможная ручная модификация первично созданного шаблона.

**Пример файла кофигурации, содержащий секции для всех поддерживаемых СУБД:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Exporter": {
    "StorageType": 2,
    "LogFolder": "C:\\Users\\akpaev.e.ENTERPRISE\\Desktop\\1Cv8Log",
    "Portion": 10000,
    "TimeZone": "Europe/Moscow",
    "WritingMaxDegreeOfParallelism": 1,
    "CollectedFactor": 2,
    "ReadingTimeout": 1,
    "LoadArchive": false
  },
  "ClickHouse": {
    "ConnectionString": "Host=192.168.0.93;Port=8123;Database=upp_main_el;Username=default;password=;"
  },
  "ElasticSearch": {
    "Nodes": [
      {
        "Host": "http://192.168.0.95:9200",
        "AuthenticationType": "0"
      }
    ],
    "Index": "upp-main-el",
    "Separation": "M",
    "MaximumRetries": 2,
    "MaxRetryTimeout": 30
  }
}
```

### Использование:
Все приложения могут быть запущены в 2 режимах: как обычное приложение, либо как служба Windows/Linux. Для теста в Вашей среде, достаточно просто выполнить конфигурацию приложения в файле *appsettings.json*, установить runtime .net core 3.1 (при его отсутствии) и запустить exe/dll. Базы данных в СУБД вручную создавать не нужно, они будут созданы автоматически.

Для запуска приложения как службы необходимо (название службы и путь к исполняемому файлу подставить свои):</br>

**Windows:**</br>
Поместить файлы приложения в каталог и выполнить в консоли команду:
```
sc create EventLogExporter binPath= "C:\elexporter\EventLogExporter.exe"
```
и запустить службу командой:
```
sc start EventLogExporter
```
**Linux: (на примере Ubuntu 20.04.1 LTS)**:</br>
*В этом примере файлы приложения были помещены в каталог /opt/EventLogExporter*</br>
В /etc/systemd/system создать файл eventlogexporter.service с содержимым:
```
[Service]
Type=notify
WorkingDirectory=/opt/EventLogExporter
ExecStart=/usr/bin/dotnet /opt/EventLogExporter/EventLogExporter.dll

[Install]
WantedBy=multi-user.target
```
Применить изменения командой:
``` 
systemctl daemon-reload
```
и запустить службу:
```
systemctl start eventlogexporter.service
```

### Результаты тестирования:
Для теста был использован сервер с Intel Xeon E5-2643 3.40 GHz x2, 128 GB RAM и SAS дисками (Windows Server 2016). Экземпляр ElasticSearch установлен на хосте, экземпляр ClickHouse развернут на нем же в виртуальной машине (Hyper-V) с 4096 MiB RAM. Размер загружаемого журнала регистрации - 945 MiB.</br>

|СУБД         |Порция|Время загрузки  |Потребляемая память  |Событий/сек  |MiB/сек  |Итоговый размер таблицы|
|:-----------:|:----:|:--------------:|:-------------------:|:-----------:|:-------:|:---------------------:|
|ClickHouse   |10000 |1 мин. 41 сек.  | ~ 60 MiB            |71032        |9.13     |56.66 MiB              |
|ElasticSearch|5000  |2 мин. 35 сек.  | ~ 100 MiB           |45968        |6.09     |1106.7 MiB             |

ClickHouse использовался as is, но на колонки (в зависимости от типа и состава данных) были выставлены кодеки. Для шаблона индекса ElasticSearch были выставлены параметры number_of_shards = 6, number_of_replicas = 0, index.codec = best_compression и использовалось 4 потока записи.
