# Модульность без микросервисов

Исходники доклада для DotNext.

## Ветки

**`main`** — код в том виде, в каком он был на докладе. `module-base/` и `modular-analyzer/`
лежат в репозитории как проекты, на которые ссылаются все три демо.

**`modulith-packages`** (эта ветка) — то же самое, но базовый класс, анализаторы и
MSBuild-конвенции подключаются пакетом [Modulith](https://github.com/gandjustas/modulith) вместо
вендоренных проектов.

## Что изменилось на этой ветке

- `module-base/` и `modular-analyzer/` удалены; вместо них одна `PackageReference` в каждом
  `Directory.Packages.props`.
- `ModuleBase` переехал в namespace `Modulith`, поэтому в модулях появился `using Modulith;`.
- `AppContext` в `modular-data` собирает модель из реестра активированных модулей
  (`ModuleBase.GetLoadedModules`), а не из `AppDomain.CurrentDomain.GetAssemblies()`. AppDomain
  показывает и те сборки, на которые сослались, но которые ни разу не активировали.

Три вещи нашли новые правила анализатора, и все три были настоящими:

- **MOD0007** — `RazorModule` и `MvcModule` регистрировали себя как `IStartupFilter` повторно,
  хотя `ModuleBase` делает это сам. `Configure` выполнялся дважды.
- **MOD0001** — `public readonly record struct AddRequest` в `RemoteRmq`. Прежний анализатор
  проверял только `TypeKind.Class` и структуру пропускал.
- **MOD0008** — `RemoteRmq` регистрирует hosted-сервис. Здесь это намеренно (обработчик RPC
  специально живёт в одном процессе с вызывающим, чтобы померить стоимость круга через RabbitMQ),
  поэтому предупреждение подавлено с объяснением.

Плюс одно исправление, не связанное с пакетом: топология `orders` в `modular-data` не
запускалась. `OrdersMicroservice` использовал `Customer` из `Customers.Entities` как DTO, а
значит зависел от этого модуля, который в этой топологии не загружается. Теперь у него свой
`CustomerDto` — что и есть правильный микросервисный дизайн: сервис, который делит тип сущности
с сервисом, который вызывает, имеет не контракт, а связанность.

## Сборка

Пакет пока не опубликован на nuget.org, поэтому `NuGet.config` указывает на локальный фид:

```bash
cd ../modulith && dotnet pack src/Modulith/Modulith.csproj -c Release -o local-feed
```
