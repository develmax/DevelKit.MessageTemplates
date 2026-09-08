# DevelKit.MessageTemplates.DynamicsCrm

Адаптер Dynamics CRM 2015: .NET Framework 4.5.2, Microsoft.CrmSdk.CoreAssemblies 7.0.0.1. Это отдельный проект и пакет; SQL Server ему не нужен.

## Подключение

```csharp
using DevelKit.MessageTemplates;
using DevelKit.MessageTemplates.Query;
using DevelKit.MessageTemplates.DynamicsCrm;

var schema = new TemplateSchema()
    .AddEntity("appointment", "activityid", "ownerid", "scheduledstart")
    .AddEntity("systemuser", "systemuserid", "fullname")
    .AddRelationship("appointment", "ownerid", "systemuser",
        new RelationshipStep("systemuser", "ownerid", "systemuserid"));

var engine = new TemplateEngine(schema, new CrmTemplateDataProvider(service));
var result = await engine.RenderAsync(
    "{{appointment:ownerid{{systemuser:fullname}}}}: {{appointment:scheduledstart(dd.MM.yyyy)}}",
    new Dictionary<string, object> { ["appointment"] = appointmentId });
```

service — IOrganizationService приложения. Подключение, аутентификация, права и срок жизни сервиса остаются у вызывающего кода.

TemplateSchema явно задаёт доступные сущности, поля, первичные ключи и связи. CrmQueryTranslator переводит план в настоящие типы Microsoft.Xrm.Sdk.Query. CrmTemplateDataProvider вызывает RetrieveMultiple, затем разворачивает AliasedValue, OptionSetValue и Money; EntityReference превращается в Guid. Даты сохраняются без автоматического перевода часового пояса.

Связи через activityparty задаются цепочкой RelationshipStep с фильтром participationtypemask. Фиксированной модели организации в адаптере нет.

Для псевдонимов используйте CrmTemplateAliasRepository с именами сущности и полей вашего каталога. Его можно обернуть в CachedTemplateAliases. Репозиторий читает страницы по PagingCookie, включая неактивные записи для семантики отключённых параметров.

## Границы

- CRM 2015 SDK синхронный. ReadAsync соответствует контракту ядра, но сетевой вызов остаётся синхронным. Отмена проверяется до и после него; прерывание уже выполняющегося вызова не обещается.
- Проверки используют fake IOrganizationService и реальные типы SDK. Живое подключение и работа в plugin sandbox не проверены.
- Это библиотека приложения, не зарегистрированный CRM-плагин. Подпись и развёртывание plugin assembly не выполнялись.
- Современный Dataverse ServiceClient не входит в эту реализацию.
- Схему настройте до параллельного использования.
- Ограничения выбора одной записи и форматирования описаны в [документации совместимости](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/compatibility.md).

[Официальный пакет CRM SDK](https://www.nuget.org/packages/Microsoft.CrmSdk.CoreAssemblies/7.0.0.1).
