using FusionModules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// Контекст для dotnet ef — против объединения всех модулей.
//
// dotnet ef поднимает хост, поэтому HostingStartup отрабатывает и модель повторяет
// HOSTINGSTARTUPASSEMBLIES той оболочки, из которой запустили команду: переменная не выставлена —
// пустая миграция, выставлена — таблицы одной топологии, и ошибки нет ни там, ни там. Фабрика
// называет модули сама, а имена берёт из KnownModules, который FusionModules генерирует из ссылок
// этого проекта. Список, написанный руками, — копия csproj, которую никто не сверяет.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppContext>
{
    public AppContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(ModuleBase.CreateModuleRegistry(KnownModules.Names))
            .Build();

        var options = new DbContextOptionsBuilder<AppContext>()
            .UseNpgsql(configuration.GetConnectionString("Postgres"))
            .Options;

        return new AppContext(options, configuration);
    }
}
