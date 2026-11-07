using FusionModules;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

// Три топологии в одном процессе — ровно тот случай, в котором
// AppDomain.CurrentDomain.GetAssemblies() начинает врать. В процессе одного хоста он точен, но
// в тестовом процессе загружены модули всех топологий сразу, поэтому каждая видит объединение.
// Реестр сообщает только то, что активировалось в этом хосте.
public class ModuleRegistryTests
{
    sealed class Factory(string modules) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(WebHostDefaults.HostingStartupAssembliesKey, modules);
            base.ConfigureWebHost(builder);
        }
    }

    // Сборка приложения активируется первой и в переменной не указана — она и в списке первая.
    // Остальные модули друг на друга не ссылаются, поэтому идут по имени, каким бы ни был порядок
    // в переменной.
    [Test]
    [Arguments("ApiModule", "Host,ApiModule")]
    [Arguments("MvcModule;ApiModule", "Host,ApiModule,MvcModule")]
    [Arguments("ApiModule;RazorModule;MvcModule", "Host,ApiModule,MvcModule,RazorModule")]
    [Arguments("RazorModule;MvcModule;ApiModule", "Host,ApiModule,MvcModule,RazorModule")]
    public async Task RegistryReportsHostFirst_ThenModulesByName(string modules, string expected)
    {
        using var factory = new Factory(modules);
        _ = factory.Server;

        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var loaded = string.Join(',', ModuleBase.GetLoadedModules(configuration).Select(a => a.GetName().Name));

        await Assert.That(loaded).IsEqualTo(expected);
    }
}
