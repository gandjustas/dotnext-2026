using System.Net.Http.Json;
using System.Text.Json;

namespace Tests;

public class Tests
{
    [ClassDataSource<WebApplicationFactory>(Shared = SharedType.PerTestSession)]
    public required WebApplicationFactory WebApplicationFactory { get; init; }

    [Test]
    public async Task Test()
    {
        var client = WebApplicationFactory.CreateClient();

        var response = await client.GetAsync("/host");

        var stringContent = await response.Content.ReadAsStringAsync();

        await Assert.That(stringContent).IsEqualTo("Hello from Host!");
    }

    [Test]
    public async Task WeatherForecastTest()
    {
        var client = WebApplicationFactory.CreateClient();

        var response = await client.GetAsync("/weatherforecast");

        var content = await response.Content.ReadFromJsonAsync<WeatherForecast[]>();
        await Assert.That(content).IsNotNull();
        await Assert.That(content.Length).IsEqualTo(5);
    }

    record WeatherForecast(DateOnly Date, int TemperatureC, int TemperatureF, string? Summary);
}
