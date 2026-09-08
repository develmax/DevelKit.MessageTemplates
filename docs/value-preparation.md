# Подготовка значений перед форматированием

[English](en/value-preparation.md)

SqlServerQueryGenerator строит запрос. SqlServerTemplateDataProvider выполняет его и читает первую строку. PreparingTemplateDataProvider загружает метаданные выбранных полей, добавляет колонки смещения времени и приводит значения к заданным CLR-типам.

Подключение:

    var source = new SqlServerTemplateDataProvider(connection, "dbo", transaction);
    var provider = new PreparingTemplateDataProvider(source, valueMetadata, schema);
    var engine = new TemplateEngine(schema, provider);
    var text = await engine.RenderAsync(template, targets, aliases);

Соединение уже открыто. Соединением и транзакцией владеет приложение. Один экземпляр SQL-провайдера рассчитан на последовательные вызовы. valueMetadata реализует ITemplateValueMetadataProvider, schema задаёт разрешённые поля и связи.

## Порядок работы

1. Движок раскрывает параметры и строит план запроса.
2. Декоратор передаёт в LoadAsync список TemplateResultField: исходная сущность, поле и имя колонки результата.
3. Поставщик метаданных возвращает правило для каждой колонки, включая явный Raw, если преобразование не требуется.
4. Декоратор проверяет служебные поля по схеме и добавляет их в копию запроса.
5. SQL-провайдер выполняет параметризованную команду.
6. Декоратор преобразует значения и возвращает исходные колонки.
7. TemplateEngine сопоставляет колонки с узлами через TemplateMapping. Затем форматтер вычисляет if и применяет форматы.

Обе ветки if входят в план. Преобразование выполняется до выбора ветки. Например, SQL Int64 приводится к объявленному Int32 до сравнения с целым литералом. Переполнение вызывает ошибку. Правила самого форматтера не меняются.

## Метаданные и время

Примеры правил:

    new TemplateValueMetadata(TemplateValueKind.Int32);
    new TemplateValueMetadata(TemplateValueKind.String, NullSentinel: "UNKNOWN");
    new TemplateValueMetadata(TemplateValueKind.UtcDateTime,
        OffsetColumn: "utcOffset", FallbackTimeZone: applicationTimeZone);

NullSentinel действует только на конкретное поле, без учёта регистра. null и DBNull остаются пустыми значениями. Строки не преобразуются в числа неявно. Ссылки представлены Guid, отображаемое имя нужно запросить отдельно.

UtcDateTime означает, что DateTime без Kind из SQL содержит UTC. DateTimeOffset переводится в UTC. DateTimeKind.Local отклоняется: адаптер должен явно определить исходную зону. После применения смещения результат имеет Kind.Unspecified, поскольку время получателя не является локальным временем сервера.

Смещение задаётся в минутах, от −840 до 840. Если выбранная колонка содержит null, применяется FallbackTimeZone. Без неё дата остаётся UTC. Если колонка вообще не возвращена, возникает ошибка. Для сезонных изменений используйте TimeZoneInfo либо передавайте смещение, рассчитанное на дату события.

OffsetColumn может быть корневым полем или alias.field существующей связи. Декоратор добавляет колонку, но не создаёт отсутствующие связи. Если зона находится в отдельном справочнике, адаптер приложения должен включить эту связь в план либо предоставить смещение через представление источника. Выбор между зоной клиента, интереса и настройками организации остаётся правилом приложения.

Метаданные получают из доверенного каталога. Они не заменяют разрешения пользователя и проверку схемы. LoadAsync получает весь набор полей одного запроса. Кеширование и его обновление определяет реализация каталога.

## Dynamics CRM

    var valueMetadata = new CrmTemplateValueMetadataProvider(service,
        (field, rule) => field.Field == "firstname"
            ? rule with { NullSentinel = "UNKNOWN" }
            : rule);
    var provider = new PreparingTemplateDataProvider(
        new CrmTemplateDataProvider(service), valueMetadata, schema);
    var engine = new TemplateEngine(schema, provider);

CRM-провайдер раскрывает AliasedValue, OptionSetValue, Money и EntityReference. Поставщик метаданных получает типы через RetrieveAttributeRequest. Декоратор применяет общие правила после раскрытия SDK-обёрток. В пределах одного вызова одинаковое поле запрашивается один раз.

Адаптер собирается с SDK 7.0 и .NET Framework 4.5.2. В этом SDK нет DateTimeBehavior. По умолчанию атрибут DateTime трактуется как UTC. Исключения для конкретных полей задаются через configure, например правилом Raw. Проверки SDK выполняются с тестовым IOrganizationService, без подключения к рабочей CRM.

Полный SQL-сценарий с каталогом параметров, текстами, метаданными, двумя клиентами и проверкой результата: [NotificationFlow](../samples/NotificationFlow/Program.cs). Транслитерация, ограничения канала и отправка выполняются приложением после подготовки текста.
