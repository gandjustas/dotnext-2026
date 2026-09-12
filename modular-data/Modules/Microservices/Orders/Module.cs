using Microsoft.EntityFrameworkCore;
using Orders;
using System.Collections.Frozen;
using System.Text.Json;
using FusionModules;

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
                var customers = await http.GetFromJsonAsync<CustomerDto[]>(
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

// The customers service's response, as this module sees it. Deliberately not Customers.Entities'
// Customer: using that type would make this module depend on that module, and ModuleBase would
// then refuse to start a topology that has one without the other — correctly, because a
// compile-time reference to a module is a statement about deployment.
//
// It is also the right microservice design. A service that shares an entity type with the
// service it calls does not have a contract, it has a coupling.
record CustomerDto(int Id, string Name, string Email);
