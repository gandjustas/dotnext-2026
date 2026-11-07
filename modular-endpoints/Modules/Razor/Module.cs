using FusionModules;

[assembly: HostingStartup(typeof(RazorModule.Module))]
namespace RazorModule;

class Module : ModuleBase
{
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddRazorPages().AddApplicationPart(typeof(Module).Assembly);
    }

    protected override void Configure(IApplicationBuilder builder)
    {
        builder.UseEndpoints(app =>
        {
            app.MapRazorPages();
        });
    }
}
