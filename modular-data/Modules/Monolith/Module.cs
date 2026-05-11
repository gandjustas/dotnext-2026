using Microsoft.EntityFrameworkCore;
using Customers;
using Orders;

[assembly: HostingStartup(typeof(Module))]

class Module : ModuleBase
{
    protected override void Configure(IApplicationBuilder builder)
    {
        builder.UseEndpoints(endpoints =>
        {
            var group = endpoints.MapGroup("/orders")
                                 .WithName("Orders");

            group.MapGet("/unpaid", (DbContext ctx) =>
            {
                return from o in ctx.Set<Order>()
                       join c in ctx.Set<Customer>() on o.CustomerId equals c.Id
                       where o.PaymentDate == null
                       where o.OrderDate < DateTime.UtcNow.AddDays(-3)
                       select new
                       {
                           OrderId = o.Id,
                           o.OrderDate,
                           Customer = new
                           {
                               c.Id,
                               c.Name,
                               c.Email
                           }
                       };
            })
            .WithName("Unpaid");

            endpoints.MapPost("/seed", async (DbContext ctx, CancellationToken ct) =>
            {

                var customers = ctx.Set<Customer>();
                var customerIds = await customers.Select(e => e.Id).ToListAsync(ct);

                if (customerIds.Count == 0)
                {
                    for (int i = 1; i <= 100; i++)
                    {
                        customers.Add(new()
                        {
                            Id = i,
                            Name = $"Customer {i}",
                            Email = $"customer{i}@mail.ru",
                        });
                        customerIds.Add(i);
                    }
                }

                var orders = ctx.Set<Order>();

                if (!await orders.AnyAsync(ct))
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        orders.Add(new()
                        {
                            CustomerId = customerIds[Random.Shared.Next(customerIds.Count)],
                            OrderDate = DateTime.UtcNow.AddMinutes(-Random.Shared.Next(10_000)),
                            Amount = Random.Shared.Next(1000) + 1,
                        });
                    }
                }

                await ctx.SaveChangesAsync(ct);
            });
        });
    }
}