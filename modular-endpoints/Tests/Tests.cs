using System.Net;
using System.Net.Http.Json;
using TUnit.AspNetCore;

namespace Tests;

public class ApiTopologyTests : WebApplicationTest<ApiTopology, Program>
{
    [Test]
    public async Task Test()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/host");

        var stringContent = await response.Content.ReadAsStringAsync();

        await Assert.That(stringContent).IsEqualTo("Hello from Host!");
    }

    [Test]
    public async Task WeatherForecastTest()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/weatherforecast");

        var content = await response.Content.ReadFromJsonAsync<WeatherForecast[]>();
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsEqualTo(5);
    }

    // Топология, которая не назвала MvcModule, его маршрутов не отдаёт — при том что
    // ProjectReference на него у хоста есть.
    [Test]
    public async Task MvcRoutesAreNotServed()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/MvcModule/Home/Index");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    record WeatherForecast(DateOnly Date, int TemperatureC, int TemperatureF, string? Summary);
}

public class MvcTopologyTests : WebApplicationTest<MvcTopology, Program>
{
    // Контроллер модуля internal, и находит его AllowInternalControllers. Компиляция об этом
    // ничего не скажет: маршрут либо есть в живом процессе, либо его нет.
    [Test]
    public async Task InternalControllerIsDiscovered()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/MvcModule/Home/Index");

        response.EnsureSuccessStatusCode();
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Welcome");
    }

    [Test]
    public async Task InternalViewModelSurvivesTheRoundTrip()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/MvcModule/Home/Error");

        response.EnsureSuccessStatusCode();
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Error");
    }
}

public class RazorTopologyTests : WebApplicationTest<RazorTopology, Program>
{
    [Test]
    public async Task PageIsServed()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Welcome");
    }

    // Статика модуля живёт в _content/<AssemblyName>/, а не в корне: два модуля могут
    // привезти свой site.css и не подраться. Страница обязана ссылаться туда же.
    [Test]
    public async Task ModuleScriptIsServedFromItsOwnPrefix()
    {
        var client = Factory.CreateClient();

        var page = await client.GetStringAsync("/");
        await Assert.That(page).Contains("_content/RazorModule/js/razormodule");

        var script = await client.GetAsync("/_content/RazorModule/js/razormodule.js");
        script.EnsureSuccessStatusCode();

        var rootPath = await client.GetAsync("/js/razormodule.js");
        await Assert.That(rootPath.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
