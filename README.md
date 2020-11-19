# Инструменты для чтения и экспорта журнала регистрации 1С
Репозиторий содержит как библиотеки так и готовые инструменты для чтения и экспорта журнала регистрации 1С в ClickHouse и ElasticSearch.

## Roadmap:
1. Реализовать менеджер экспортёров с автоматическим подключением к экспорту логов новых баз и удалением обработанных файлов

## Состав:
* [OneSTools.EventLog](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog) - Библиотека для чтения журнала регистрации (старый формат, LGF и LGP файлы). Позволяет выполнять как разовое чтение данных, так и запуск в "live" режиме</br>
* [OneSTools.EventLog.Exporter.Core](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.Core) - Библиотека-ядро для инструментов экспорта журнала регистрации, на основе которой можно создавать приложения для экспорта в новые СУБД.</br>
* [OneSTools.EventLog.Exporter.ClickHouse](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.ClickHouse) - Базовый пакет, реализующий интерфейс IEventLogStorage для экспорта журнала регистрации 1С в [ClickHouse](https://clickhouse.tech/)</br>
* [EventLogExporterClickHouse](https://github.com/akpaevj/OneSTools.EventLog/tree/master/EventLogExporterClickHouse) - Служба для экспорта жрунала регистрации в [ClickHouse](https://clickhouse.tech/)</br>
* [OneSTools.EventLog.Exporter.ElasticSearch](https://github.com/akpaevj/OneSTools.EventLog/tree/master/OneSTools.EventLog.Exporter.ElasticSearch) - Базовый пакет, реализующий интерфейс IEventLogStorage для экспорта журнала регистрации 1С в [ElasticSearch](https://www.elastic.co/)</br>
* [EventLogExporterElasticSearch](https://github.com/akpaevj/OneSTools.EventLog/tree/master/EventLogExporterElasticSearch) - Служба для экспорта жрунала регистрации в [ElasticSearch](https://www.elastic.co/)</br>

## Get started:

### Конфигурация:
В файле конфигурации (appsettings.json) у приложений есть общая часть, не зависящая от СУБД:
```json
"Exporter": {
    "LogFolder": "C:\\Users\\akpaev.e.ENTERPRISE\\Desktop\\1Cv8Log",
    "Portion": 10000,
    "LoadArchive": false
  }
```
где:</br>
* LogFolder - путь к каталогу журнала регистрации 1С.</br>
* Portion - Размер порции, записываемый в БД за одну итерацию (10000 по умолчанию)</br>
* LoadArchive - Специальный параметр, предназначенный для первоначальной загрузки архивных данных. При установке параметра в true, отключется "live" режим и не происходит запроса последнего обработанного файла из БД</br>

А так-же есть настройки для конкретной СУБД, примеры которых приведены ниже. Для работы нужно подставить значения для Вашей среды:</br>

**ClickHouse:**
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

**Полный файл конфигурации на примере ClickHouse:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=8123;Username=default;password=;Database=database_name;"
  },
  "Exporter": {
    "LogFolder": "C:\\Program Files\\1cv8\\srvinfo\\reg_1541\\d0d55cdc-d47d-431f-8612-210d67093d14\\1Cv8Log",
    "Portion": 10000
  }
}
```

### Использование:
Все приложения могут быть запущены в 2 режимах: как обычное приложение, либо как служба Windows/Linux. Для теста в Вашей среде, достаточно просто выполнить конфигурацию приложения в файле *appsettings.json*, установить runtime .net core 3.1 (при его отсутствии) и запустить exe/dll. Базы данных в СУБД вручную создавать не нужно, они будут созданы автоматически.

*Примечание по Kiban'е:
По умолчанию в кибане параметр временной зоны установлен в "Browser". Из-за этого дата отображается с часовым сдвигом, соответствующем зоне браузера. Что-бы избежать этого, переставьте зону на UTC*

Для запуска приложения как службы необходимо (название службы и путь к исполняемому файлу подставить свои):</br>

**Windows:**</br>
Поместить файлы приложения в каталог и выполнить в консоли команду:
```
sc create EventLogExporterClickHouse binPath= "C:\elexporterch\EventLogExporterClickHouse.exe"
```
и запустить службу командой:
```
sc start EventLogExporterClickHouse
```
**Linux: (на примере Ubuntu 20.04.1 LTS)**:</br>
*В этом примере файлы приложения были помещены в каталог /opt/EventLogExporterClickHouse*</br>
В /etc/systemd/system создать файл eventlogexporterclickhouse.service с содержимым:
```
[Service]
Type=notify
WorkingDirectory=/opt/EventLogExporterClickHouse
ExecStart=/usr/bin/dotnet /opt/EventLogExporterClickHouse/EventLogExporterClickHouse.dll

[Install]
WantedBy=multi-user.target
```
Применить изменения командой:
``` 
systemctl daemon-reload
```
и запустить службу:
```
systemctl start eventlogexporterclickhouse.service
```

### Результаты тестирования:
Для теста был использован сервер с Intel Xeon E5-2643 3.40 GHz x2, 128 GB RAM и SAS дисками (Windows Server 2016). Экземпляр ElasticSearch установлен на хосте, экземпляр ClickHouse развернут на нем же в виртуальной машине (Hyper-V) с 4096 MiB RAM. Размер загружаемого журнала регистрации - 3216 MiB (29.765.282 событий).</br>

|СУБД         |Порция|Время загрузки  |Потребляемая память  |Событий/сек  |MiB/сек  |Итоговый размер таблицы|
|:-----------:|:----:|:--------------:|:-------------------:|:-----------:|:-------:|:---------------------:|
|ClickHouse   |10000 |7 мин. 28 сек.  | ~ 60 MiB            |66440        |7.18     |14.34 MiB              |
|ElasticSearch|5000  |13 мин. 21 сек. | ~ 60 MiB            |37160        |4.02     |2023.2 MiB             |

ClickHouse использовался as is, но на колонки (в зависимости от типа и состава данных) были выставлены кодеки. Для шаблона индекса ElasticSearch были выставлены параметры number_of_shards = 6, number_of_replicas = 0, index.codec = best_compression.
