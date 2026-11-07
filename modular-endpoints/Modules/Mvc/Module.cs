using FusionModules;

[assembly: HostingStartup(typeof(MvcModule.Module))]
namespace MvcModule;
class Module : ModuleBase
{
    public const string AreaName = nameof(MvcModule);
    protected override void ConfigureServices(IHostApplicationBuilder builder)
    {
        // AllowInternalControllers: MVC ищет только публичные контроллеры, а публичный контроллер
        // тянет за собой в public и сервисы своего конструктора, и модели своих действий.
        builder.Services.AddControllersWithViews()
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