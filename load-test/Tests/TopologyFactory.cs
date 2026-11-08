using FusionModules.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Tests;

// Топология — это набор модулей, и тест выбирает его тем же способом, что и деплой:
// HOSTINGSTARTUPASSEMBLIES (docker-compose.yml). Бэкендов здесь нет: вопрос тестов — что собралось,
// а не до чего дотянулось. Поэтому фоновые сервисы модулей сняты (обработчик RPC в RemoteRmq
// подключался бы к RabbitMQ), а строка подключения к RabbitMQ разбирается, но никуда не ведёт: EasyNetQ
// подключается лениво. Остаётся GenericWebHostService — он строит пайплайн, а с ним и проверки FusionModules.
public class TopologyFactory(params string[] modules) : ModularWebApplicationFactory<Program>(modules)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseConnectionStrings(("Rabbit", "host=127.0.0.1;port=1")).WithoutApplicationHostedServices();
        base.ConfigureWebHost(builder);
    }
}
