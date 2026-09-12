using Modulith;
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

        // Реестр активированных модулей, а не AppDomain: AppDomain показывает всё, что
        // случайно оказалось загружено в процессе, включая сборки, на которые сослались, но
        // которые ни разу не активировали.
        foreach (var assembly in ModuleBase.GetLoadedModules(configuration))
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }
}