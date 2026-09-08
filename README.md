# DevelKit.MessageTemplates

Шаблонизатор сообщений для .NET: поля сущностей, переходы по связям, условия, форматы .NET и каталог именованных параметров. Подходит для SMS, email, push и других текстовых уведомлений. Библиотека подготавливает текст. Отправку и правила конкретного канала реализует приложение.

[![Build](https://github.com/develmax/DevelKit.MessageTemplates/actions/workflows/build.yml/badge.svg)](https://github.com/develmax/DevelKit.MessageTemplates/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/LICENSE)

## Пакеты

| Пакет | Платформа | Назначение |
| --- | --- | --- |
| DevelKit.MessageTemplates | net452; net10.0 | Парсер, форматтер, каталог параметров, кеш и независимый план чтения |
| DevelKit.MessageTemplates.DynamicsCrm | net452 | Адаптер Dynamics CRM 2015 SDK 7.0.0.1 |
| DevelKit.MessageTemplates.SqlServer | net10.0 | Генерация SQL Server и параметризованного DbCommand |

Адаптеры зависят от ядра и подключаются отдельно. Ядро не имеет внешних runtime-зависимостей. Текущая версия: **0.1.0-preview.3**. Публичная публикация в NuGet пока не выполнена. Пакеты можно собрать из исходников и подключить из локального источника.

## Быстрый пример без базы

```csharp
using DevelKit.MessageTemplates;

var template = TextTemplateParser.Parse(
    "Здравствуйте, {{person:name}}! Встреча {{meeting:startsAt(dd.MM.yyyy HH:mm)}}.");

// Значения можно получить из уже загруженных данных приложения.
var text = TextTemplateFormatter.Format(template, parameter =>
    parameter.FieldName switch
    {
        "name" => "Анна",
        "startsAt" => new DateTime(2026, 9, 15, 14, 30, 0),
        _ => null
    });

Console.WriteLine(text);
// Здравствуйте, Анна! Встреча 15.09.2026 14:30.
```

Парсер и форматтер можно использовать независимо от адаптеров. Для загрузки данных по шаблону TemplateEngine строит план и передаёт его ITemplateDataProvider. Допустимые сущности, поля и связи задаются через TemplateSchema или собственный IQueryMetadata.

## Язык шаблонов

```text
Поле: {{person:name}}
Формат: {{meeting:startsAt(dd.MM.yyyy HH:mm)}}
Связь: {{meeting:guest{{person:name}}}}
Условие: {{meeting:if[[isOnline = true ? meetingUrl ; address]]}}
```

Именованный параметр раскрывается перед разбором текста:

```csharp
var aliases = new[]
{
    new TemplateAlias("{{meeting:date}}", "{{meeting:startsAt(dd.MM.yyyy)}}")
};
var expanded = TemplateAliases.Expand("Ждём вас {{meeting:date}}.", aliases);
```

Каталог можно загружать через ITemplateAliasRepository и кешировать с CachedTemplateAliases. После изменения каталога вызывайте Invalidate. В этой версии ключ псевдонима имеет вид {{entity:field}} без пробелов внутри имени сущности или поля.

## Сборка и проверка

Для полного запуска нужны Windows, PowerShell 7, .NET SDK 10 и .NET Framework 4.8 для запуска приложений net452. Зависимости восстанавливаются с nuget.org.

```powershell
./build.ps1
```

Скрипт собирает решение в Release, выполняет проверки ядра, SQL и CRM, запускает два примера и создаёт три пакета в artifacts. Примеры и тесты работают без серверов и учётных данных. GitHub Actions запускает тот же сценарий на Windows.

## Документация и примеры

- [Синтаксис и этапы обработки](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/syntax.md).
- [Как читать код](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/code-guide.md).
- [Поведение и ограничения](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/compatibility.md).
- [CRM-адаптер](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/dynamics-crm.md).
- [Пример SQL](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/samples/Meetings/Program.cs) и [пример CRM SDK](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/samples/CrmMeetings/Program.cs).
- [Сборка и выпуск пакетов](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/release.md).
- [Участие в разработке](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/CONTRIBUTING.md).

## Границы применения

Шаблон выбирает данные только в пределах схемы, разрешённой приложением. Проверка SQL-идентификаторов не заменяет контроль доступа. Значения SQL передаются через DbParameter. Обе ветви if загружаются до выбора результата.

Парсер мягкий: нераспознанные фрагменты могут остаться текстом. HTML-кодирование, лимиты длины, стоимость SMS и часовые пояса задаёт приложение. План рассчитан на одну строку данных, без циклов по коллекциям. Живые CRM и SQL Server, CRM plugin sandbox и современный Dataverse не входят в автоматическую проверку.

## Лицензия

[MIT](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/LICENSE).
Copyright (c) 2026 Maksim Moiseev (develmax).

Разрешены модификация и коммерческое использование. При распространении сохраняйте уведомление об авторских правах и текст лицензии.
