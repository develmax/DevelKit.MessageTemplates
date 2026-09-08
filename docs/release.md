# Сборка и выпуск

**Русский** | [English](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/release.md)

## Локальная проверка

На Windows установите PowerShell 7, .NET SDK 10 и .NET Framework 4.8, затем выполните из корня репозитория:

```powershell
./build.ps1
```

Скрипт восстанавливает зависимости, собирает все целевые платформы, выполняет консольные проверки ядра/SQL и CRM, запускает оба примера и создаёт пакеты в artifacts. Используются демонстрационный IOrganizationService и данные в памяти. Реальные серверы не нужны.

Проверки запускаются через dotnet run и exe net452. Это консольные сценарии, поэтому одного dotnet test недостаточно. GitHub Actions выполняет тот же build.ps1 и сохраняет nupkg как артефакты запуска.

## Локальный источник NuGet

После сборки подключите абсолютный путь к artifacts в проекте-потребителе:

```powershell
dotnet nuget add source "<absolute-path-to-artifacts>" --name DevelKitLocal
dotnet add package DevelKit.MessageTemplates.SqlServer --version 0.1.0
```

Для CRM используйте DevelKit.MessageTemplates.DynamicsCrm в проекте net452. Ядро приходит транзитивно. Доступ к nuget.org нужен для восстановления Microsoft.CrmSdk.CoreAssemblies.

## Подготовка новой версии

1. Обновите Version в Directory.Build.props и документацию версии.
2. Запустите build.ps1.
3. Проверьте содержимое трёх nupkg: DLL, XML-документация, README, LICENSE, автор, MIT и RepositoryUrl.
4. Проверьте пакет в отдельном проекте через PackageReference.

Сборка и push исходников не публикуют пакеты в NuGet. Публикация выполняется отдельно владельцем репозитория после проверки версии и прав на PackageId. Не добавляйте API-ключи в исходники или NuGet.Config.

## Метаданные

Все три пакета используют лицензию MIT, автора Maksim Moiseev (develmax) и адрес репозитория https://github.com/develmax/DevelKit.MessageTemplates. Версия SDK CRM зафиксирована отдельно в проекте адаптера.
