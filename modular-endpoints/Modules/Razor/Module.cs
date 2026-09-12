using Modulith;

[assembly: HostingStartup(typeof(RazorModule.Module))]
namespace RazorModule;

class Module : ModuleBase
{
    protected override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services.AddRazorPages().AddApplicationPart(typeof(Module).Assembly);
    }

    protected override void Configure(IApplicationBuilder builder)
    {
        builder.UseEndpoints(app =>
        {
            app.MapRazorPages();
        });
    }
}
