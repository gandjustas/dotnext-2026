[assembly: HostingStartup(typeof(MvcModule.Module))]
namespace MvcModule;
class Module : ModuleBase
{
    public const string AreaName = nameof(MvcModule);
    protected override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services.AddControllersWithViews().AddApplicationPart(typeof(Module).Assembly);
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