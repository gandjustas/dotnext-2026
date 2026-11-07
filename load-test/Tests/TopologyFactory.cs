using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tests;

// Топология — это набор модулей, и тест выбирает его тем же способом, что и деплой:
// HOSTINGSTARTUPASSEMBLIES (docker-compose.yml). Бэкендов здесь нет: вопрос тестов — что собралось,
// а не до чего дотянулось. Поэтому фоновые сервисы модулей сняты (обработчик RPC в RemoteRmq
// подключался бы к RabbitMQ), а строка подключения к RabbitMQ — заглушка: EasyNetQ подключается
// лениво. Остаётся GenericWebHostService — он строит пайплайн, а с ним и проверки FusionModules.
public class TopologyFactory(params string[] modules) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.HostingStartupAssembliesKey, string.Join(';', modules));
        builder.UseSetting("ConnectionStrings:Rabbit", "host=127.0.0.1;port=1");

        builder.ConfigureTestServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(IHostedService) &&
                                     d.ImplementationType?.FullName != "Microsoft.AspNetCore.Hosting.GenericWebHostService")
                         .ToList())
            {
                services.Remove(descriptor);
            }
        });
    }
}
