using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;

namespace Tests;

// Топология — это набор модулей, и тест выбирает его тем же способом, что и деплой:
// HOSTINGSTARTUPASSEMBLIES (docker-compose.yml). Фабрика — TUnit: каждый тест получает свой хост, логи
// хоста попадают в вывод теста, а клиенты несут его traceparent.
public abstract class Topology(params string[] modules) : TestWebApplicationFactory<Program>
{
    public IReadOnlyList<string> Modules { get; } = modules;

    // Модули хост выбирает при старте, поэтому это стартовая конфигурация, а не ConfigureAppConfiguration.
    protected override void ConfigureStartupConfiguration(IConfigurationBuilder configuration) =>
        configuration.AddInMemoryCollection([new(WebHostDefaults.HostingStartupAssembliesKey, string.Join(';', Modules))]);

    // ASPNETCORE_HOSTINGSTARTUPASSEMBLIES в окружении теста ASP.NET Core загрузит вдобавок к названным
    // модулям, и тест проверял бы другую топологию. Поэтому — отказ, а не тихая подмена.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES")))
        {
            throw new InvalidOperationException("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES is set in the test process: unset it, each test names its topology itself.");
        }

        base.ConfigureWebHost(builder);
    }
}

// Тесты, которые называют модули сами, — по топологии на каждый набор аргументов. Хост поднимается
// внутри теста, поэтому и отказ стартовать — обычное исключение, которое тест проверяет. TUnit
// освобождает хосты после теста.
public abstract class TopologyTest<TFactory> : PerTestWebApplicationTest<TFactory, Program>
    where TFactory : Topology, new()
{
    protected Task<TracedWebApplicationFactory<Program>> StartAsync(params string[] modules) =>
        CreateFactoryAsync(builder => builder.UseSetting(WebHostDefaults.HostingStartupAssembliesKey, string.Join(';', modules)));
}
