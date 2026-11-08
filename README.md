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

**`fusionmodules`** — то же самое на пакете
[FusionModules](https://www.nuget.org/packages/FusionModules)
([исходники](https://github.com/gandjustas/FusionModules)) версии 0.3.0: базовый класс, анализаторы и
MSBuild-конвенции приходят одной `PackageReference`, а с ними — проверки топологии при старте и MOD0014
при сборке, модель EF Core из модулей пакетом `FusionModules.EntityFrameworkCore`, тесты топологий на
`FusionModules.Testing`.

**`fusionmodules-v0.3-tunit`** (эта ветка) — `fusionmodules`, в которой тесты топологий подняты на
[TUnit.AspNetCore](https://tunit.dev/docs/examples/aspnet): фабрика `TestWebApplicationFactory` и базовые
классы тестов TUnit вместо `ModularWebApplicationFactory`, TUnit 1.81.10.

## Как устроено на FusionModules

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
  `ConfigureServices(WebHostBuilderContext, IServiceCollection)`, которая в пакете устарела (CS0672).
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
- `Web.csproj` в `load-test` перечисляет `Contracts.ICalculator` в
  `fusion_modules_allowed_service_collisions`: четыре транспорта — альтернативы (см.
  [«Проверки топологии»](#проверки-топологии)).
- `modular-data` собирает модель пакетом `FusionModules.EntityFrameworkCore`, тесты всех трёх демо —
  на TUnit.AspNetCore с расширениями `FusionModules.Testing`.

Попутно исправлено то, что не связано с пакетом:

- `Index.cshtml` в `RazorModule` ссылался на `~/js/razormodule.js`, а статика модуля отдаётся из
  `_content/RazorModule/`: кнопка «Say hello» не делала ничего.
- `Microsoft.AspNetCore.OpenApi` поднят до 10.0.14: 10.0.7 тянул `Microsoft.OpenApi` 2.0.0 с уязвимостью
  (NU1903), и сборка с `-warnaserror` падала.

### Данные: `modular-data`

Модель EF Core собирает пакет `FusionModules.EntityFrameworkCore`, а контекст остаётся в три строки:

```csharp
builder.Services.AddDbContext<AppContext>(b => b
    .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
    .UseModules(builder.Configuration));

class AppContext(DbContextOptions<AppContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyModuleConfigurations(this);
}
```

`UseModules` берёт модули из `ModuleBase.GetLoadedModules` — реестра модулей, которые активировал
именно этот хост. `AppDomain.CurrentDomain.GetAssemblies()` даёт тот же ответ, пока процессом владеет
один хост, и врёт, когда их несколько — в любой сборке интеграционных тестов загружены модули всех
топологий. По той же причине `UseModules` подменяет ключ кеша модели: EF Core кеширует модель по типу
контекста, и без ключа, учитывающего модули, вторая топология в процессе молча получила бы модель
первой. В `main` то же самое делали руками `ModuleFingerprint` и `ModuleAwareModelCacheKeyFactory` в
`Web/Program.cs`; на модель переход не повлиял — `dotnet ef migrations has-pending-model-changes`
изменений не видит.

Какие модули дали какие сущности, пакет пишет в лог один раз на набор модулей:

```text
Model of AppContext: configurations from: Customers.Entities (Customer), Orders.Entities (Order),
Monolith (Order); configured by several modules: Order (Orders.Entities, then Monolith)
```

`dotnet ef` поднимает хост, поэтому без фабрики модель миграции повторяла бы
`HOSTINGSTARTUPASSEMBLIES` той оболочки, где запустили команду: не выставлена — пустая миграция,
выставлена — таблицы одной топологии, и ни ошибки, ни предупреждения.
[`DesignTimeDbContextFactory`](modular-data/Web/DesignTimeDbContextFactory.cs) называет модули сам —
`UseModules(KnownModules.Names)`, имена из `KnownModules`, который пакет генерирует из ссылок `Web`
(в `main` список был написан в фабрике руками — копия csproj, которую никто не сверяет). Проверено:
`dotnet ef dbcontext script` с `HOSTINGSTARTUPASSEMBLIES=Orders.Entities` выдаёт обе таблицы и внешний
ключ, `dotnet ef migrations has-pending-model-changes` — изменений нет.

**Порядок модулей.** `GetLoadedModules` отдаёт каждый модуль после модулей, на которые ссылается его
сборка, а в остальном — по имени, и в этом порядке `ApplyModuleConfigurations` применяет конфигурации. Ссылки берутся
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

## Проверки топологии

**Два модуля с разными реализациями одного сервиса не стартуют.** Без этой проверки топология,
которая по ошибке назвала два транспорта `load-test`, стартовала бы, и контроллер получил бы
калькулятор того модуля, что назван последним. С ней старт падает:

```text
Two modules register the same service with different implementations. ...
  Contracts.ICalculator
    Local: Local.Calculator
    RemoteGrpc: RemoteGrpc.Calculator
Contracts.ICalculator is listed in the host's build as implemented by alternative modules
(fusion_modules_allowed_service_collisions): load only one of Local, RemoteGrpc in a topology —
remove the others from HOSTINGSTARTUPASSEMBLIES.
```

**MOD0014** задаёт тот же вопрос при сборке хоста — по всем модулям, на которые он ссылается. `Web` в
`load-test` ссылается на все четыре транспорта, и MOD0014 предупреждает про `ICalculator`. Здесь это
намеренно: транспорты — альтернативы одного ребра, и вместе их не грузит ни одна топология. Поэтому
`Web.csproj` говорит об этом сборке:

```xml
<fusion_modules_allowed_service_collisions>Contracts.ICalculator</fusion_modules_allowed_service_collisions>
```

Разрешение не позволяет грузить транспорты вместе — оно говорит, что они альтернативы. Топологию,
которая всё-таки назовёт два, остановит проверка при старте, и `ValidateModules()`, сгенерированный из
этого же списка, добавит к ошибке последний абзац выше: загружайте один.

**Строка `Topology:` в логе** — что собралось в этом процессе:

```text
Topology: modules: Customers.Entities, Orders.Entities, Monolith; endpoints: Monolith 2 (none from
Customers.Entities, Orders.Entities)
```

Модули сущностей эндпоинтов не отдают, и строка называет их отдельно; для модуля, который должен
отвечать на запросы, такое «none from» — это 404, ради которого строку и стоит читать.

**Предупреждения при старте** о маршрутах, которые отдают два модуля, — например, `Monolith` и
`OrdersMicroservice` в одном процессе отдают `GET /orders/unpaid` и ответили бы 500, — и об options,
которые по-разному настраивают несколько модулей. В топологиях демо их нет.

**`FusionModules.EntityFrameworkCore`** — модель из модулей в `modular-data` (см. выше).

**`FusionModules.Testing`** — расширения для хоста топологии; фабрика в этой ветке своя, на TUnit (см.
[«Тесты»](#тесты)). `UseConnectionStrings(("Postgres", "Host=127.0.0.1;Port=1;…"))` —
строки подключения, которые разбираются, но никуда не ведут (для RabbitMQ в `load-test` —
`host=127.0.0.1;port=1`); `Database:Migrate=false` тесты `modular-data` ставят сами, это ключ
приложения; `WithoutApplicationHostedServices()` снимает фоновые сервисы модулей;
`Services.Routes()` — маршруты хоста с модулем, который каждый отдаёт (`host` — у маршрутов самого
приложения), без ручного обхода `EndpointDataSource`.

## Тесты

Каждое демо поднимает хост в процессе теста с модулями, названными так же, как в
`docker-compose.yml`, — через `HOSTINGSTARTUPASSEMBLIES`.

| Демо | Что проверяют |
|---|---|
| `load-test` | у каждой из четырёх топологий есть `ICalculator`, и он из модуля этой топологии; локальный транспорт считает настоящим запросом; без транспорта калькулятора нет; опечатка в имени модуля не стартует; два транспорта в одном процессе не стартуют |
| `modular-data` | таблицы, схемы и внешние ключи каждой топологии; три топологии в одном процессе — каждая со своей моделью; монолит в любом порядке модулей — та же модель и тот же объект в кеше; маршруты каждой топологии и только они |
| `modular-endpoints` | топология на каждый модуль (internal-контроллер найден, скрипт `RazorModule` отдаётся из `_content`, чужих маршрутов нет), монолит со всеми модулями и какой модуль какие маршруты в нём отдаёт, топология без модулей, опечатка в имени модуля, порядок модулей в реестре не зависит от переменной |

Хост поднимает `TestWebApplicationFactory<Program>` из TUnit.AspNetCore: у каждого теста свой хост, логи
хоста (и строка `Topology:`) попадают в вывод теста, а клиенты несут `traceparent` теста. Модули
выбирает `Topology` в `Tests/Topology.cs` каждого демо — ключом `HOSTINGSTARTUPASSEMBLIES` в стартовой
конфигурации, а при `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES` в окружении теста отказывается стартовать.

- Топология, общая для класса тестов (`modular-endpoints`), — тип фабрики: `ApiTopology`,
  `AllModulesTopology` и так далее, класс тестов наследует `WebApplicationTest<ApiTopology, Program>`.
- Топология из аргументов теста — `TopologyTest<TFactory>.StartAsync("Local")`: хост поднимается
  внутри теста, поэтому отказ стартовать (опечатка в имени модуля, два транспорта) — обычное
  исключение, которое тест проверяет. Так написаны тесты `load-test` и `modular-data`.

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

Пакеты FusionModules ставятся с nuget.org, как любые другие: ни `NuGet.config`, ни локального фида не
нужно. Поэтому образы собираются тем же `docker compose`, контекст сборки — папка демо.

```bash
docker compose -f modular-data/docker-compose.yml build
```

```bash
docker compose -f load-test/docker-compose.yml build
```

Топологии `load-test` и `modular-data` поднимаются своими `docker-compose.yml`: один образ, разный
`HOSTINGSTARTUPASSEMBLIES` у каждого сервиса.
