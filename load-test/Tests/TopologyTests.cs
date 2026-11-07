using Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

// Четыре топологии из docker-compose.yml — один и тот же Web, разный транспорт до калькулятора.
// Сборка про топологии не знает ничего: какой модуль загружен, решает переменная окружения, поэтому
// и проверяется живой процесс.
public class TopologyTests
{
    public static IEnumerable<Func<string>> Transports() =>
        new[] { "Local", "RemoteRest", "RemoteGrpc", "RemoteRmq" }.Select(module => (Func<string>)(() => module));

    // TestController берёт ICalculator конструктором. Контейнер такую зависимость контроллера не
    // проверяет — без транспорта топология стартует и падает на первом запросе. Поэтому вопрос
    // «есть ли у топологии то, что она просит» задаётся явно, для каждой.
    [Test]
    [MethodDataSource(nameof(Transports))]
    public async Task EachTopology_HasACalculator(string module)
    {
        await using var factory = new TopologyFactory(module);

        var isService = factory.Services.GetRequiredService<IServiceProviderIsService>();

        await Assert.That(isService.IsService(typeof(ICalculator))).IsTrue();
    }

    // Реализация — из модуля этой топологии, а не чужого. RemoteRmq не резолвится: шине для этого
    // нужен живой RabbitMQ.
    [Test]
    [Arguments("Local")]
    [Arguments("RemoteRest")]
    [Arguments("RemoteGrpc")]
    public async Task TheCalculator_ComesFromTheTopologysModule(string module)
    {
        await using var factory = new TopologyFactory(module);
        await using var scope = factory.Services.CreateAsyncScope();

        var calculator = scope.ServiceProvider.GetRequiredService<ICalculator>();

        await Assert.That(calculator.GetType().Assembly.GetName().Name).IsEqualTo(module);
    }

    // Локальный транспорт — единственный, которому не нужен бэкенд: настоящий запрос насквозь.
    [Test]
    public async Task Local_AddsInProcess()
    {
        await using var factory = new TopologyFactory("Local");
        using var client = factory.CreateClient();

        var sum = await client.GetStringAsync("/test?a=2&b=3");

        await Assert.That(sum).IsEqualTo("5");
    }

    [Test]
    public async Task WithoutATransport_ThereIsNoCalculator()
    {
        await using var factory = new TopologyFactory();

        var isService = factory.Services.GetRequiredService<IServiceProviderIsService>();

        await Assert.That(isService.IsService(typeof(ICalculator))).IsFalse();
    }

    // Транспорты — альтернативы, и в одном процессе их не грузит ни одна топология из
    // docker-compose.yml. Если загрузить два, процесс стартует, а контроллер получит калькулятор того
    // модуля, что назван последним: последняя регистрация ICalculator выигрывает. FusionModules 0.2
    // такое не ловит ни при сборке, ни при старте, поэтому тест фиксирует, что происходит на деле.
    [Test]
    [Arguments("Local;RemoteGrpc", "RemoteGrpc")]
    [Arguments("RemoteGrpc;Local", "Local")]
    public async Task TwoTransportsInOneProcess_TheLastOneWins(string modules, string winner)
    {
        await using var factory = new TopologyFactory(modules.Split(';'));
        await using var scope = factory.Services.CreateAsyncScope();

        var calculator = scope.ServiceProvider.GetRequiredService<ICalculator>();

        await Assert.That(calculator.GetType().Assembly.GetName().Name).IsEqualTo(winner);
    }

    // Опечатка в HOSTINGSTARTUPASSEMBLIES — без FusionModules это процесс, который стартует, проходит
    // пробу и отвечает 404. Здесь он не стартует и называет имя.
    [Test]
    public async Task AMisspelledModule_DoesNotStart()
    {
        await using var factory = new TopologyFactory("Local", "RemoteGprc");

        var failure = await Assert.That(() => factory.Services).Throws<InvalidOperationException>();

        await Assert.That(failure!.Message).Contains("RemoteGprc");
    }
}
