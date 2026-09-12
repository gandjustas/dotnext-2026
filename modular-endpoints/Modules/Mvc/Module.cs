using FusionModules;

[assembly: HostingStartup(typeof(MvcModule.Module))]
namespace MvcModule;
class Module : ModuleBase
{
    public const string AreaName = nameof(MvcModule);
    protected override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        // AllowInternalControllers: MVC's stock discovery requires a public controller, and a
        // public controller cannot take an internal service or return an internal model without
        // dragging both public with it. The module keeps its types to itself instead.
        services.AddControllersWithViews()
                .AddApplicationPart(typeof(Module).Assembly)
                .AllowInternalControllers();
    }

    protected override void Configure(IApplicationBuilder builder)
    {
        builder.UseEndpoints(app =>
        {
            app.MapControllerRoute(
                name: "default",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
        });
    }
}