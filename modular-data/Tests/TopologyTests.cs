using FusionModules.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

// Топология — набор модулей, выбранный тем же способом, что и в docker-compose.yml:
// HOSTINGSTARTUPASSEMBLIES. Базы нет: модель EF собирается без подключения, строка подключения
// никуда не ведёт, а миграции на старте выключены — Database:Migrate=false, как у реплик, которые
// не мигрируют.
public class TopologyFactory(params string[] modules) : ModularWebApplicationFactory<Program>(modules)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseConnectionStrings(("Postgres", "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused"))
            .UseSetting("Database:Migrate", "false");
        base.ConfigureWebHost(builder);
    }

    /// <summary>Таблицы модели этой топологии: «схема.таблица», и внешние ключи «откуда → куда».</summary>
    public (string[] Tables, string[] ForeignKeys) Model()
    {
        using var scope = Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<DbContext>().Model;

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
    public string ModelDebugView()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DbContext>().Model.ToDebugString(MetadataDebugStringOptions.LongDefault);
    }

    /// <summary>Модель EF Core этой топологии — тот самый объект, что лежит в кеше моделей.</summary>
    public Microsoft.EntityFrameworkCore.Metadata.IModel ModelInstance()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DbContext>().Model;
    }

    /// <summary>Маршруты топологии: «МЕТОД /шаблон (модуль)».</summary>
    public string[] Routes() => [.. Services.Routes().Select(route => route.ToString())];
}

// Три топологии из docker-compose.yml. Модель у каждой своя: монолит связывает заказы с клиентами
// внешним ключом, микросервисы — каждый со своей таблицей и без чужих.
public class TopologyTests
{
    private static readonly string[] Monolith = ["Orders.Entities", "Customers.Entities", "Monolith"];
    private static readonly string[] Orders = ["Orders.Entities", "OrdersMicroservice"];
    private static readonly string[] Customers = ["Customers.Entities", "CustomersMicroservice"];

    [Test]
    public async Task Monolith_HasBothTables_AndTheForeignKey()
    {
        await using var factory = new TopologyFactory(Monolith);

        var (tables, foreignKeys) = factory.Model();

        await Assert.That(tables).IsEquivalentTo(new[] { "Marketing.Customers", "Sales.Orders" });
        await Assert.That(foreignKeys).IsEquivalentTo(new[] { "Orders → Customers" });
    }

    [Test]
    public async Task Orders_HasOnlyItsTable()
    {
        await using var factory = new TopologyFactory(Orders);

        var (tables, foreignKeys) = factory.Model();

        await Assert.That(tables).IsEquivalentTo(new[] { "Sales.Orders" });
        await Assert.That(foreignKeys).IsEmpty();
    }

    [Test]
    public async Task Customers_HasOnlyItsTable()
    {
        await using var factory = new TopologyFactory(Customers);

        var (tables, _) = factory.Model();

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
        await using var canonical = new TopologyFactory(Monolith);
        await using var reordered = new TopologyFactory(modules.Split(';'));

        await Assert.That(reordered.Model().ForeignKeys).IsEquivalentTo(new[] { "Orders → Customers" });
        await Assert.That(reordered.ModelDebugView()).IsEqualTo(canonical.ModelDebugView());
    }

    // Ключ кеша модели строится по упорядоченным модулям, поэтому перестановки одного набора — это
    // одна модель, а не по модели на каждый порядок. Без сортировки здесь были бы два разных объекта.
    [Test]
    public async Task Monolith_InAnotherOrder_SharesTheCachedModel()
    {
        await using var canonical = new TopologyFactory(Monolith);
        await using var reordered = new TopologyFactory("Monolith", "Customers.Entities", "Orders.Entities");

        await Assert.That(ReferenceEquals(reordered.ModelInstance(), canonical.ModelInstance())).IsTrue();
    }

    // Как в любой сборке интеграционных тестов: несколько топологий в одном процессе. EF Core кеширует
    // модель по типу контекста, и без ключа, который учитывает модули, вторая топология получила бы
    // модель первой — молча, с чужими таблицами.
    [Test]
    public async Task ThreeTopologiesInOneProcess_EachHasItsOwnModel()
    {
        await using var monolith = new TopologyFactory(Monolith);
        await using var orders = new TopologyFactory(Orders);
        await using var customers = new TopologyFactory(Customers);

        await Assert.That(monolith.Model().Tables).IsEquivalentTo(new[] { "Marketing.Customers", "Sales.Orders" });
        await Assert.That(orders.Model().Tables).IsEquivalentTo(new[] { "Sales.Orders" });
        await Assert.That(customers.Model().Tables).IsEquivalentTo(new[] { "Marketing.Customers" });
    }

    [Test]
    public async Task EachTopology_ServesItsRoutes_AndOnlyThose()
    {
        await using var monolith = new TopologyFactory(Monolith);
        await using var orders = new TopologyFactory(Orders);
        await using var customers = new TopologyFactory(Customers);

        await Assert.That(monolith.Routes()).IsEquivalentTo(new[] { "GET /orders/unpaid (Monolith)", "POST /seed (Monolith)" });
        await Assert.That(orders.Routes()).IsEquivalentTo(new[] { "GET /orders/unpaid (OrdersMicroservice)" });
        await Assert.That(customers.Routes()).IsEquivalentTo(new[] { "GET /customers (CustomersMicroservice)" });
    }
}
