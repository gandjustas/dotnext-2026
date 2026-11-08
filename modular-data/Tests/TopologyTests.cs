using FusionModules.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

// Web без модулей и без базы; модули тест называет сам. Модель EF собирается без подключения, строка
// подключения никуда не ведёт, а миграции на старте выключены — Database:Migrate=false, как у реплик,
// которые не мигрируют.
public sealed class NoDatabase() : Topology()
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseConnectionStrings(("Postgres", "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused"))
            .UseSetting("Database:Migrate", "false");
        base.ConfigureWebHost(builder);
    }
}

public static class TopologyModel
{
    /// <summary>Таблицы модели этой топологии: «схема.таблица», и внешние ключи «откуда → куда».</summary>
    public static (string[] Tables, string[] ForeignKeys) Model(this IServiceProvider services)
    {
        var model = services.ModelInstance();

        var tables = model.GetEntityTypes()
            .Select(e => $"{e.GetSchema()}.{e.GetTableName()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        var foreignKeys = model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Select(fk => $"{fk.DeclaringEntityType.GetTableName()} → {fk.PrincipalEntityType.GetTableName()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        return (tables, foreignKeys);
    }

    /// <summary>Вся модель EF Core текстом: сущности, свойства, ключи, связи и аннотации.</summary>
    public static string ModelDebugView(this IServiceProvider services) =>
        services.ModelInstance().ToDebugString(MetadataDebugStringOptions.LongDefault);

    /// <summary>Модель EF Core этой топологии — тот самый объект, что лежит в кеше моделей.</summary>
    public static Microsoft.EntityFrameworkCore.Metadata.IModel ModelInstance(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DbContext>().Model;
    }

    /// <summary>Маршруты топологии: «МЕТОД /шаблон (модуль)».</summary>
    public static string[] RouteList(this IServiceProvider services) => [.. services.Routes().Select(route => route.ToString())];
}

// Три топологии из docker-compose.yml. Модель у каждой своя: монолит связывает заказы с клиентами
// внешним ключом, микросервисы — каждый со своей таблицей и без чужих.
public class TopologyTests : TopologyTest<NoDatabase>
{
    private static readonly string[] Monolith = ["Orders.Entities", "Customers.Entities", "Monolith"];
    private static readonly string[] Orders = ["Orders.Entities", "OrdersMicroservice"];
    private static readonly string[] Customers = ["Customers.Entities", "CustomersMicroservice"];

    [Test]
    public async Task Monolith_HasBothTables_AndTheForeignKey()
    {
        var topology = await StartAsync(Monolith);

        var (tables, foreignKeys) = topology.Services.Model();

        await Assert.That(tables).IsEquivalentTo(new[] { "Marketing.Customers", "Sales.Orders" });
        await Assert.That(foreignKeys).IsEquivalentTo(new[] { "Orders → Customers" });
    }

    [Test]
    public async Task Orders_HasOnlyItsTable()
    {
        var topology = await StartAsync(Orders);

        var (tables, foreignKeys) = topology.Services.Model();

        await Assert.That(tables).IsEquivalentTo(new[] { "Sales.Orders" });
        await Assert.That(foreignKeys).IsEmpty();
    }

    [Test]
    public async Task Customers_HasOnlyItsTable()
    {
        var topology = await StartAsync(Customers);

        var (tables, _) = topology.Services.Model();

        await Assert.That(tables).IsEquivalentTo(new[] { "Marketing.Customers" });
    }

    // Порядок модулей в HOSTINGSTARTUPASSEMBLIES на модель не влияет. Сравнивается вся модель, а не
    // только таблицы. Этот тест прошёл бы и в порядке переменной: Monolith только добавляет связь, а
    // fluent API накопительный. Порядок решает, когда связывающий модуль переопределяет настройку
    // модуля сущностей; что GetLoadedModules от переменной не зависит, проверяет следующий тест.
    [Test]
    [Arguments("Monolith;Customers.Entities;Orders.Entities")]
    [Arguments("Monolith;Orders.Entities;Customers.Entities")]
    [Arguments("Customers.Entities;Monolith;Orders.Entities")]
    public async Task Monolith_InAnyOrder_HasTheSameModel(string modules)
    {
        var canonical = await StartAsync(Monolith);
        var reordered = await StartAsync(modules.Split(';'));

        await Assert.That(reordered.Services.Model().ForeignKeys).IsEquivalentTo(new[] { "Orders → Customers" });
        await Assert.That(reordered.Services.ModelDebugView()).IsEqualTo(canonical.Services.ModelDebugView());
    }

    // Ключ кеша модели строится по упорядоченным модулям, поэтому перестановки одного набора — это
    // одна модель, а не по модели на каждый порядок. Без сортировки здесь были бы два разных объекта.
    [Test]
    public async Task Monolith_InAnotherOrder_SharesTheCachedModel()
    {
        var canonical = await StartAsync(Monolith);
        var reordered = await StartAsync("Monolith", "Customers.Entities", "Orders.Entities");

        await Assert.That(ReferenceEquals(reordered.Services.ModelInstance(), canonical.Services.ModelInstance())).IsTrue();
    }

    // Как в любой сборке интеграционных тестов: несколько топологий в одном процессе. EF Core кеширует
    // модель по типу контекста, и без ключа, который учитывает модули, вторая топология получила бы
    // модель первой — молча, с чужими таблицами.
    [Test]
    public async Task ThreeTopologiesInOneProcess_EachHasItsOwnModel()
    {
        var monolith = await StartAsync(Monolith);
        var orders = await StartAsync(Orders);
        var customers = await StartAsync(Customers);

        await Assert.That(monolith.Services.Model().Tables).IsEquivalentTo(new[] { "Marketing.Customers", "Sales.Orders" });
        await Assert.That(orders.Services.Model().Tables).IsEquivalentTo(new[] { "Sales.Orders" });
        await Assert.That(customers.Services.Model().Tables).IsEquivalentTo(new[] { "Marketing.Customers" });
    }

    [Test]
    public async Task EachTopology_ServesItsRoutes_AndOnlyThose()
    {
        var monolith = await StartAsync(Monolith);
        var orders = await StartAsync(Orders);
        var customers = await StartAsync(Customers);

        await Assert.That(monolith.Services.RouteList()).IsEquivalentTo(new[] { "GET /orders/unpaid (Monolith)", "POST /seed (Monolith)" });
        await Assert.That(orders.Services.RouteList()).IsEquivalentTo(new[] { "GET /orders/unpaid (OrdersMicroservice)" });
        await Assert.That(customers.Services.RouteList()).IsEquivalentTo(new[] { "GET /customers (CustomersMicroservice)" });
    }
}
