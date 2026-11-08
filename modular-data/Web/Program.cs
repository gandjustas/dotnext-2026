using FusionModules.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args).ValidateModules();

var services = builder.Services;
services.AddHttpClient();
services.AddServiceDiscovery();
services.ConfigureHttpClientDefaults(http =>
{
    http.AddServiceDiscovery();
});

builder.Services.AddDbContext<AppContext>(b =>
    b.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
     // Топология с частью модулей имеет модель меньше снэпшота миграций — это законно.
     // Понижаем до лога, а не подавляем: на полной модели предупреждение по-прежнему что-то значит.
     .ConfigureWarnings(o => o.Log(RelationalEventId.PendingModelChangesWarning))
     // Модули, которые активировал этот хост, — в опциях контекста; по ним же ключ кеша модели.
     .UseModules(builder.Configuration)
);

if (builder.Environment.IsDevelopment())
{
    services.ConfigureDbContext<AppContext>(b =>
        b.EnableDetailedErrors().EnableSensitiveDataLogging()
    );
}

services.AddTransient<DbContext>(sp => sp.GetRequiredService<AppContext>());
var app = builder.Build();

// Миграции катит одна реплика; остальные и тесты топологий (им нужна модель, а не база) ставят
// Database:Migrate=false (в окружении — Database__Migrate).
if (app.Configuration.GetValue("Database:Migrate", defaultValue: true))
{
    await using var scope = app.Services.CreateAsyncScope();
    using var ctx = scope.ServiceProvider.GetRequiredService<AppContext>();
#if DEBUG
    Console.WriteLine(ctx.Model.ToDebugString(MetadataDebugStringOptions.LongDefault));
#endif

    await ctx.Database.MigrateAsync();
}

app.UseRouting();
await app.RunAsync();

// Модель — из конфигураций модулей, которые активировал хост (ModuleBase.GetLoadedModules): каждый
// модуль после модулей, на которые ссылается его сборка, остальные по имени. Поэтому Monolith с его
// внешним ключом применяется после Orders.Entities и Customers.Entities, в каком бы порядке их ни
// назвал HOSTINGSTARTUPASSEMBLIES.
//
// Модули берутся из реестра этого хоста, а не из AppDomain: в сборке интеграционных тестов загружены
// модули всех топологий сразу. И модель кешируется по набору модулей, а не только по типу контекста —
// иначе вторая топология в процессе молча получила бы модель первой. Это делает UseModules.
class AppContext(DbContextOptions<AppContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyModuleConfigurations(this);
}
