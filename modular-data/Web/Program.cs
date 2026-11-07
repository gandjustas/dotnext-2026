using FusionModules;
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
     .ReplaceService<IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>()
);

if (builder.Environment.IsDevelopment())
{
    services.ConfigureDbContext<AppContext>(b =>
        b.EnableDetailedErrors().EnableSensitiveDataLogging()
    );
}

services.AddTransient<DbContext>(sp => sp.GetRequiredService<AppContext>());
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
using (var ctx = scope.ServiceProvider.GetRequiredService<AppContext>())
{
#if DEBUG
    Console.WriteLine(ctx.Model.ToDebugString(MetadataDebugStringOptions.LongDefault));
#endif

    await ctx.Database.MigrateAsync();
}

app.UseRouting();
await app.RunAsync();

class AppContext(DbContextOptions options, IConfiguration configuration) : DbContext(options)
{
    // Модули, которые реально активировались в этом хосте. GetLoadedModules отдаёт каждый модуль
    // после модулей, на которые ссылается его сборка, а в остальном — по имени: Monolith всегда идёт
    // после Orders.Entities и Customers.Entities, и порядок в HOSTINGSTARTUPASSEMBLIES на модель не влияет.
    readonly IReadOnlyList<System.Reflection.Assembly> modules = ModuleBase.GetLoadedModules(configuration);

    // Состав модулей, из которых собрана модель. Входит в ключ кэша модели EF Core; топологии с
    // одним набором модулей в разном порядке делят одну модель.
    public string ModuleFingerprint => string.Join(';', modules.Select(a => a.GetName().Name));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var assembly in modules)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }
}

// EF Core кэширует построенную модель по типу контекста, во внутреннем сервис-провайдере,
// общем для всех контекстов с одинаковыми опциями. Два хоста с разными наборами модулей
// в одном процессе — то есть любая сборка интеграционных тестов — иначе молча делят
// модель первого: ничего не падает, просто не те таблицы.
class ModuleAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => context is AppContext app
        ? (typeof(AppContext), app.ModuleFingerprint, designTime)
        : (object)(context.GetType(), designTime);
}
