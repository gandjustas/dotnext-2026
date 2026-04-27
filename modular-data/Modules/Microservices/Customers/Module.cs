
using Customers;
using Microsoft.EntityFrameworkCore;

[assembly:HostingStartup(typeof(Module))]

class Module : IHostingStartup, IStartupFilter
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(this);
        });
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => (builder) =>
    {
        next(builder);
        builder.UseEndpoints(endpoints => endpoints.MapGet("/customers",
            (int[] id, DbContext ctx) => ctx.Set<Customer>().Where(c => id.Contains(c.Id)))
        );
    };
}