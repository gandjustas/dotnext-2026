using FusionModules;
using FusionModules.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// dotnet ef поднимает хост приложения и честно активирует модули из HOSTINGSTARTUPASSEMBLIES.
// Ровно в этом и проблема: без фабрики состав миграции определяется переменной окружения той
// оболочки, в которой запустили команду. Переменная не задана — активируется только сборка
// приложения, и создаётся ПУСТАЯ миграция без единой ошибки. Задана подмножеством — создаётся
// миграция для подмножества, и тоже без ошибок. Оба исхода молчат и оба зависят от машины.
//
// Фабрика называет состав сама, поэтому миграции одинаковы у всех и в CI. Миграции всегда
// генерируются по объединению всех модулей и применяются целиком.
//
// Имена — из KnownModules, который FusionModules генерирует из ссылок этого проекта: список,
// написанный руками, — копия csproj, которую никто не сверяет. От KnownModules нужен только набор:
// порядок, в котором применяются конфигурации, UseModules строит так же, как в рантайме, — по
// ссылкам загруженных сборок.
class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppContext>
{
    public AppContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<AppContext>()
            .UseNpgsql(configuration.GetConnectionString("Postgres"))
            .UseModules(KnownModules.Names)
            .Options;

        return new AppContext(options);
    }
}
