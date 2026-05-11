using Customers;
using Microsoft.EntityFrameworkCore;
using Orders;
using System.Collections.Frozen;
using System.Text.Json;

[assembly: HostingStartup(typeof(Module))]

class Module : ModuleBase
{ 
    protected override void Configure(IApplicationBuilder builder)
    {
        builder.UseEndpoints(endpoints =>
        {
            var group = endpoints.MapGroup("/orders")
                                 .WithName("Orders");

            group.MapGet("/unpaid", async (DbContext ctx, HttpClient http, CancellationToken ct) =>
            {
                var ordersQuery = from o in ctx.Set<Order>()
                                  where o.PaymentDate == null
                                  where o.OrderDate < DateTime.UtcNow.AddDays(-3)
                                  select new
                                  {
                                      o.Id,
                                      o.OrderDate,
                                      o.CustomerId
                                  };
                var orders = await ordersQuery.ToListAsync(ct);
                var customerIds = orders.Select(o => o.CustomerId).Distinct();
                var customers = await http.GetFromJsonAsync<Customer[]>(
                    $"http://customers/customers?{string.Join('&', customerIds.Select(i => "id="+i))}",
                    JsonSerializerOptions.Web, ct);
                var d = customers!.ToFrozenDictionary(c => c.Id);
                return orders.Select(o =>
                {
                    var haveCustomer = d.TryGetValue(o.CustomerId, out var customer);
                    return new
                    {
                        OrderId = o.Id,
                        o.OrderDate,
                        Customer = haveCustomer ? new
                        {
                            customer!.Id,
                            customer!.Name,
                            customer!.Email,
                        } : null
                    };
                });
                       
            })
            .WithName("Unpaid");
        });
    }
}