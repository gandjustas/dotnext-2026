# Модульность без микросервисов

Исходники доклада для DotNext 2026.

Модуль — обычная библиотека классов с `[assembly: HostingStartup]`. Какие модули загрузить, решает
переменная окружения `HOSTINGSTARTUPASSEMBLIES`, а не сборка: один образ, любая топология —
монолит, микросервисы или что-то между ними.

| Демо | Что показывает |
|---|---|
| [`load-test`](load-test) | один `Web` и четыре транспорта до калькулятора — в процессе, REST, gRPC, RabbitMQ; топологию выбирает `docker-compose.yml` |
| [`modular-data`](modular-data) | одна модель EF Core, собранная из модулей; монолит и два микросервиса из одного образа |
| [`modular-endpoints`](modular-endpoints) | minimal API, MVC и Razor Pages модулями, каждый со своими маршрутами и статикой |

## Ветки

**`main`** — код в том виде, в каком он был на докладе: базовый класс `module-base/` и анализатор
`modular-analyzer/` лежат в репозитории проектами, на которые ссылаются все три демо.

**`fusionmodules`** (эта ветка) — то же самое на пакете
[FusionModules](https://www.nuget.org/packages/FusionModules)
([исходники](https://github.com/gandjustas/FusionModules)) версии 0.2: базовый класс, анализаторы и
MSBuild-конвенции приходят одной `PackageReference`.

## Как устроено на FusionModules 0.2

Модуль:

```csharp
using FusionModules;

[assembly: HostingStartup(typeof(Local.Module))]
namespace Local;

class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
        => builder.Services.AddTransient<ICalculator, Calculator>();
}
```

Хост про модули не знает ничего, кроме того, что их надо проверить:

```csharp
var builder = WebApplication.CreateBuilder(args).ValidateModules();
```

`ValidateModules()` генерируется пакетом из ссылок хоста. Без него опечатка в
`HOSTINGSTARTUPASSEMBLIES` — это процесс, который стартует, проходит пробу и отвечает 404: ASP.NET Core
пишет критическое сообщение в лог и продолжает. С ним старт падает и называет имя. Забыть вызов не
даст MOD0012.

Что изменилось относительно `main`:

- `module-base/` и `modular-analyzer/` удалены; вместо них `PackageReference` в каждом
  `Directory.Packages.props`, у всех проектов демо. `Backend/Grpc` и `Backend/Rest` модулей не грузят,
  поэтому правила хоста (MOD0006 про `CreateSlimBuilder`, MOD0012 про `ValidateModules`) их не трогают.
- Модули на `Microsoft.NET.Sdk` вместо `Microsoft.NET.Sdk.Web` с `OutputType=Library`; модули с
  `.cshtml` (`MvcModule`, `RazorModule`) — на `Microsoft.NET.Sdk.Razor`. Неявные `using` и
  анализаторы ASP.NET Core (ASP0000, MVC1000–MVC1006), которые приносил Web SDK, возвращает
  `FusionModules.targets`.
- `ConfigureServices(IHostApplicationBuilder)` вместо
  `ConfigureServices(WebHostBuilderContext, IServiceCollection)`, которая в 0.2 устарела (CS0672).
- `Host.csproj` в `modular-endpoints` больше не выключает `GenerateMvcApplicationPartsAssemblyAttributes`
  сам — это делает пакет для любого хоста (иначе Razor SDK подключал бы контроллеры модулей в обход
  `HOSTINGSTARTUPASSEMBLIES`; MOD0007).
- `HomeController` в `MvcModule` стал `internal`: `AddControllersWithViews().AllowInternalControllers()`.
  Публичный контроллер тянет в `public` сервисы своего конструктора и модели своих действий, а за ними
  всё, что они называют. Пакет держит это с двух сторон: публичный контроллер в модуле — MOD0001,
  internal-контроллер без `AllowInternalControllers()` (или модуль без `AddApplicationPart` для своей
  сборки) — MOD0011, потому что MVC его не найдёт и маршруты ответят 404.
- `CompilerVisibleProperty IsTestProject` в `Directory.Build.props` больше не нужен: хост, модуль и
  тест-проект различает `FusionModules.targets`.

Попутно исправлено то, что не связано с пакетом:

- `Index.cshtml` в `RazorModule` ссылался на `~/js/razormodule.js`, а статика модуля отдаётся из
  `_content/RazorModule/`: кнопка «Say hello» не делала ничего.
- `Microsoft.AspNetCore.OpenApi` поднят до 10.0.14: 10.0.7 тянул `Microsoft.OpenApi` 2.0.0 с уязвимостью
  (NU1903), и сборка с `-warnaserror` падала.

### Данные: `modular-data`

Пакета для EF Core у FusionModules нет — намеренно: это пятнадцать строк в собственном контексте.
`AppContext` (`Web/Program.cs`) собирает модель из `ModuleBase.GetLoadedModules` — реестра модулей,
которые активировал именно этот хост. `AppDomain.CurrentDomain.GetAssemblies()`
даёт тот же ответ, пока процессом владеет один хост, и врёт, когда их несколько — в любой сборке
интеграционных тестов загружены модули всех топологий. По той же причине у контекста свой ключ кеша
модели (`ModuleAwareModelCacheKeyFactory`): EF Core кеширует модель по типу контекста, и без ключа,
учитывающего модули, вторая топология в процессе молча получила бы модель первой.

`dotnet ef` поднимает хост, поэтому без фабрики модель миграции повторяла бы
`HOSTINGSTARTUPASSEMBLIES` той оболочки, где запустили команду: не выставлена — пустая миграция,
выставлена — таблицы одной топологии, и ни ошибки, ни предупреждения.
[`DesignTimeDbContextFactory`](modular-data/Web/DesignTimeDbContextFactory.cs) называет модули сам, а
имена берёт из `KnownModules`, который пакет генерирует из ссылок `Web`. Проверено:
`dotnet ef dbcontext script` с `HOSTINGSTARTUPASSEMBLIES=Orders.Entities` выдаёт обе таблицы и внешний
ключ, `dotnet ef migrations has-pending-model-changes` — изменений нет.

**Порядок модулей.** `GetLoadedModules` отдаёт каждый модуль после модулей, на которые ссылается его
сборка, а в остальном — по имени, и в этом порядке `AppContext` применяет конфигурации. Ссылки берутся
из загруженных, полных сборок: у `Monolith` ссылка на `Customers.Entities` есть только там —
`Customer` встречается лишь внутри `HasOne<Customer>()`, и в reference-сборке, которую видит компилятор,
её нет. Поэтому ни порядок `HOSTINGSTARTUPASSEMBLIES`, ни `KnownModules` (это просто список по имени)
на модель не влияют, а у одного набора модулей один порядок в любой топологии и в `dotnet ef`.

Честная оговорка: в этом демо порядок на модель не влияет — fluent API EF Core накопительный, а
`Monolith` только добавляет связь. Тест держит это для трёх перестановок, сравнивая модель целиком.
Порядок решает, когда связывающий модуль переопределяет настройку модуля сущностей: если бы
`Monolith` назначал заказам другую таблицу, при `Monolith;Orders.Entities;…` выиграл бы
`Orders.Entities` (проверено). Порядок по ссылкам делает «связывающий модуль применяется последним»
гарантией, а не правилом для того, кто пишет `docker-compose.yml`. Заодно ключ кеша модели не зависит
от порядка: перестановки одного набора делят одну модель — это тоже проверяет тест.

Миграции на старте катит одна реплика; остальные (и тесты топологий, которым нужна модель, а не
база) выставляют `Database:Migrate=false` (`Database__Migrate` в окружении).

## Тесты

Каждое демо поднимает хост в процессе теста с модулями, названными так же, как в
`docker-compose.yml`, — через `HOSTINGSTARTUPASSEMBLIES`.

| Демо | Что проверяют |
|---|---|
| `load-test` | у каждой из четырёх топологий есть `ICalculator`, и он из модуля этой топологии; локальный транспорт считает настоящим запросом; без транспорта калькулятора нет; опечатка в имени модуля не стартует; два транспорта в одном процессе — выигрывает последний |
| `modular-data` | таблицы, схемы и внешние ключи каждой топологии; три топологии в одном процессе — каждая со своей моделью; монолит в любом порядке модулей — та же модель и тот же объект в кеше; маршруты каждой топологии и только они |
| `modular-endpoints` | топология на каждый модуль (internal-контроллер найден, скрипт `RazorModule` отдаётся из `_content`, чужих маршрутов нет), монолит со всеми модулями, топология без модулей, опечатка в имени модуля, порядок модулей в реестре не зависит от переменной |

Последний тест `load-test` фиксирует то, от чего FusionModules 0.2 не защищает: транспорты —
альтернативы, и если топология по ошибке назовёт два, процесс стартует, а контроллер получит
калькулятор того модуля, что назван последним.

## Сборка

```bash
dotnet build load-test/load-test.slnx -warnaserror && dotnet test --solution load-test/load-test.slnx --no-build
```

```bash
dotnet build modular-data/modular-data.slnx -warnaserror && dotnet test --solution modular-data/modular-data.slnx --no-build
```

```bash
dotnet build modular-endpoints/modular-endpoints.slnx -warnaserror && dotnet test --solution modular-endpoints/modular-endpoints.slnx --no-build
```

`dotnet test` на .NET 10 SDK работает в режиме Microsoft.Testing.Platform — он включён в `global.json`.

Пакет FusionModules ставится с nuget.org, как любой другой: ни `NuGet.config`, ни локального фида не
нужно. Поэтому образы собираются тем же `docker compose`, контекст сборки — папка демо:

```bash
docker compose -f modular-data/docker-compose.yml build
```

```bash
docker compose -f load-test/docker-compose.yml build
```

Топологии `load-test` и `modular-data` поднимаются своими `docker-compose.yml`: один образ, разный
`HOSTINGSTARTUPASSEMBLIES` у каждого сервиса.
