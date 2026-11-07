using System.Net;

namespace Tests;

public class AllModulesTopologyTests
{
    [ClassDataSource<AllModulesTopology>(Shared = SharedType.PerTestSession)]
    public required AllModulesTopology WebApplicationFactory { get; init; }

    // Монолит отдаёт всё, что отдавали топологии по одному модулю: маршруты модулей не мешают друг
    // другу, а проверки FusionModules при старте прошли — иначе фабрика не поднялась бы.
    [Test]
    [Arguments("/host")]
    [Arguments("/weatherforecast")]
    [Arguments("/MvcModule/Home/Index")]
    [Arguments("/")]
    public async Task EveryModulesRoute_IsServed(string path)
    {
        var client = WebApplicationFactory.CreateClient();

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }
}

public class NoModulesTopologyTests
{
    [ClassDataSource<NoModulesTopology>(Shared = SharedType.PerTestSession)]
    public required NoModulesTopology WebApplicationFactory { get; init; }

    [Test]
    public async Task OnlyTheHostsOwnRoute_IsServed()
    {
        var client = WebApplicationFactory.CreateClient();

        await Assert.That(await client.GetStringAsync("/host")).IsEqualTo("Hello from Host!");

        foreach (var path in new[] { "/weatherforecast", "/MvcModule/Home/Index", "/" })
        {
            var response = await client.GetAsync(path);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }
}

public class MisspelledTopologyTests
{
    // Опечатка в HOSTINGSTARTUPASSEMBLIES без FusionModules — процесс, который стартует, проходит пробу и
    // отвечает 404 на маршруты модуля. Здесь он не стартует и называет имя, которого не нашёл.
    [Test]
    public async Task AMisspelledModule_DoesNotStart()
    {
        await using var factory = new ModularWebApplicationFactory("ApiModule", "MvcModul");

        var failure = await Assert.That(() => factory.Services).Throws<InvalidOperationException>();

        await Assert.That(failure!.Message).Contains("MvcModul");
    }
}
