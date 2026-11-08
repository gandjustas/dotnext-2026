using System.Net;
using FusionModules.Testing;
using TUnit.AspNetCore;

namespace Tests;

public class AllModulesTopologyTests : WebApplicationTest<AllModulesTopology, Program>
{
    // Монолит отдаёт всё, что отдавали топологии по одному модулю: маршруты модулей не мешают друг
    // другу, а проверки FusionModules при старте прошли — иначе фабрика не поднялась бы.
    [Test]
    [Arguments("/host")]
    [Arguments("/weatherforecast")]
    [Arguments("/MvcModule/Home/Index")]
    [Arguments("/")]
    public async Task EveryModulesRoute_IsServed(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);

        response.EnsureSuccessStatusCode();
    }

    // Кто что отдаёт в монолите — по таблице эндпоинтов, а не пробами по HTTP. Статику
    // (MapStaticAssets, больше пятисот маршрутов с хешами в именах) Routes() не показывает. Маршруты
    // самого приложения — «host», хотя Host и сам модуль. У /openapi владельца нет: делегат объявлен в
    // библиотеке, а не в модуле или хосте.
    [Test]
    public async Task EachModule_ServesItsOwnRoutes()
    {
        var routes = Services.Routes().Select(route => route.ToString());

        await Assert.That(routes).IsEquivalentTo(new[]
        {
            "GET /host (host)",
            "GET /openapi/{documentName}.json",
            "GET /weatherforecast (ApiModule)",
            "* /{area:exists}/{controller=Home}/{action=Index}/{id?} (MvcModule)",
            "* / (RazorModule)",
            "* /Index (RazorModule)",
            "* /Privacy (RazorModule)",
            "* /Error (RazorModule)",
        });
    }
}

public class NoModulesTopologyTests : WebApplicationTest<NoModulesTopology, Program>
{
    [Test]
    public async Task OnlyTheHostsOwnRoute_IsServed()
    {
        var client = Factory.CreateClient();

        await Assert.That(await client.GetStringAsync("/host")).IsEqualTo("Hello from Host!");

        foreach (var path in new[] { "/weatherforecast", "/MvcModule/Home/Index", "/" })
        {
            var response = await client.GetAsync(path);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }
}

public class MisspelledTopologyTests : TopologyTest<NoModulesTopology>
{
    // Опечатка в HOSTINGSTARTUPASSEMBLIES без FusionModules — процесс, который стартует, проходит пробу и
    // отвечает 404 на маршруты модуля. Здесь он не стартует и называет имя, которого не нашёл.
    [Test]
    public async Task AMisspelledModule_DoesNotStart()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync("ApiModule", "MvcModul"));

        await Assert.That(failure!.Message).Contains("MvcModul");
    }
}
