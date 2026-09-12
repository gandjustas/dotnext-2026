using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

// dotnet ef поднимает хост приложения и честно активирует модули из HOSTINGSTARTUPASSEMBLIES.
// Ровно в этом и проблема: без фабрики состав миграции определяется переменной окружения той
// оболочки, в которой запустили команду. Переменная не задана — активируется только сборка
// приложения, и создаётся ПУСТАЯ миграция без единой ошибки. Задана подмножеством — создаётся
// миграция для подмножества, и тоже без ошибок. Оба исхода молчат и оба зависят от машины.
//
// Фабрика перечисляет состав в коде, поэтому миграции одинаковы у всех и в CI.
class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppContext>
{
    // Миграции всегда генерируются по объединению всех модулей и применяются целиком.
    // Модуль, дополняющий чужие конфигурации (Monolith), идёт последним.
    static readonly string[] AllModules = ["Customers.Entities", "Orders.Entities", "Monolith"];

    public AppContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection([
                new(WebHostDefaults.HostingStartupAssembliesKey, string.Join(';', AllModules))
            ])
            .AddInMemoryCollection(ModuleBase.CreateModuleRegistry(AllModules))
            .Build();

        var options = new DbContextOptionsBuilder<AppContext>()
            .UseNpgsql(configuration.GetConnectionString("Postgres"))
            .ReplaceService<IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>()
            .Options;

        return new AppContext(options, configuration);
    }
}
