using FusionModules.Testing;
using TUnit.Core.Interfaces;

namespace Tests;

// Топология — это набор модулей, и тест выбирает его тем же способом, что и деплой:
// HOSTINGSTARTUPASSEMBLIES. Поэтому тестовая топология и продовая — одно и то же. Хост поднимается
// заранее (IAsyncInitializer), чтобы ClassDataSource делил его между тестами одной топологии.
public class Topology(params string[] modules) : ModularWebApplicationFactory<Program>(modules), IAsyncInitializer
{
    public Task InitializeAsync()
    {
        _ = Server;

        return Task.CompletedTask;
    }
}

public sealed class ApiTopology() : Topology("ApiModule");

public sealed class MvcTopology() : Topology("MvcModule");

public sealed class RazorTopology() : Topology("RazorModule");

// Монолит: все модули в одном процессе. ValidateModules в Program.cs проверяет при старте, что каждый
// названный модуль загрузился и его стартап отработал.
public sealed class AllModulesTopology() : Topology("ApiModule", "MvcModule", "RazorModule");

// Ни одного модуля: только то, что хост делает сам (его собственный модуль активируется всегда).
public sealed class NoModulesTopology() : Topology();
