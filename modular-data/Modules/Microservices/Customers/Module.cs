
using Customers;
using Microsoft.EntityFrameworkCore;

[assembly:HostingStartup(typeof(Module))]

class Module : ModuleBase
{
    protected override void Configure(IApplicationBuilder builder) 
    {
        builder.UseEndpoints(endpoints => endpoints.MapGet("/customers",
            (int[] id, DbContext ctx) => ctx.Set<Customer>().Where(c => id.Contains(c.Id)))
        );
    }
}