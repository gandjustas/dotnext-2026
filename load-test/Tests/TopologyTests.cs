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
    // docker-compose.yml. Без проверки FusionModules топология с двумя транспортами стартовала бы, и
    // контроллер получил бы калькулятор того модуля, что назван последним. С ней старт падает и называет
    // сервис и оба модуля — в любом порядке. Разрешение для MOD0014 в Web.csproj не разрешает грузить
    // транспорты вместе, а говорит, что они альтернативы, — и ошибка старта так и говорит: грузите один.
    [Test]
    [Arguments("Local;RemoteGrpc")]
    [Arguments("RemoteGrpc;Local")]
    public async Task TwoTransportsInOneProcess_DoNotStart(string modules)
    {
        await using var factory = new TopologyFactory(modules.Split(';'));

        var failure = await Assert.That(() => factory.Services).Throws<InvalidOperationException>();

        await Assert.That(failure!.Message).Contains("Contracts.ICalculator");
        await Assert.That(failure.Message).Contains("Local: Local.Calculator");
        await Assert.That(failure.Message).Contains("RemoteGrpc: RemoteGrpc.Calculator");
        await Assert.That(failure.Message).Contains("implemented by alternative modules");
        await Assert.That(failure.Message).Contains("load only one of Local, RemoteGrpc in a topology");
        await Assert.That(failure.Message).Contains("remove the others from HOSTINGSTARTUPASSEMBLIES");
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
