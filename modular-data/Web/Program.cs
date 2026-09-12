using FusionModules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var services = builder.Services;
services.AddHttpClient();
services.AddServiceDiscovery();
services.ConfigureHttpClientDefaults(http =>
{
    // Turn on service discovery by default
    http.AddServiceDiscovery();
});

builder.Services.AddDbContext<AppContext>(b =>
    b.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
     .ConfigureWarnings(o => o.Log(RelationalEventId.PendingModelChangesWarning))
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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Реестр активированных модулей, а не AppDomain. AppDomain с фильтром по
        // HostingStartupAttribute даёт тот же ответ, пока процессом владеет один хост, и
        // перестаёт — как только его делит второй: в сборке интеграционных тестов загружены
        // модули всех топологий, поэтому каждая соберёт объединение. Ничего не упадёт, просто
        // таблицы будут не те. Плюс чужие hosting startup (IISIntegration, Application
        // Insights, browser refresh из dotnet watch) этот атрибут тоже несут.
        //
        // Порядок здесь значим: GetLoadedModules сохраняет порядок HOSTINGSTARTUPASSEMBLIES,
        // поэтому «связывающий модуль идёт последним» — правило, которое можно записать.
        foreach (var assembly in ModuleBase.GetLoadedModules(configuration))
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }
}