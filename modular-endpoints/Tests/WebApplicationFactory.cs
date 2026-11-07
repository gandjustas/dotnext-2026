using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TUnit.Core.Interfaces;

namespace Tests;

// Топология — это набор модулей, и тест выбирает его тем же способом, что и деплой:
// HOSTINGSTARTUPASSEMBLIES. Поэтому тестовая топология и продовая — одно и то же.
public class ModularWebApplicationFactory(params string[] modules)
    : WebApplicationFactory<Program>, IAsyncInitializer
{
    public Task InitializeAsync()
    {
        _ = Server;

        return Task.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.HostingStartupAssembliesKey, string.Join(';', modules));
        base.ConfigureWebHost(builder);
    }
}

public sealed class ApiTopology() : ModularWebApplicationFactory("ApiModule");

public sealed class MvcTopology() : ModularWebApplicationFactory("MvcModule");

public sealed class RazorTopology() : ModularWebApplicationFactory("RazorModule");

// Монолит: все модули в одном процессе. ValidateModules в Program.cs проверяет при старте, что каждый
// названный модуль загрузился и его стартап отработал.
public sealed class AllModulesTopology() : ModularWebApplicationFactory("ApiModule", "MvcModule", "RazorModule");

// Ни одного модуля: только то, что хост делает сам (его собственный модуль активируется всегда).
public sealed class NoModulesTopology() : ModularWebApplicationFactory();
