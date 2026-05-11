[assembly: HostingStartup(typeof(MvcModule.Module))]
namespace MvcModule;
class Module : IHostingStartup, IStartupFilter
{
    public const string AreaName = nameof(MvcModule);
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(this);
            services.AddControllersWithViews().AddApplicationPart(typeof(Module).Assembly);
        });
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
    builder =>
    {
        next(builder); // Не забыть вызвать следующий модуль
        builder.UseEndpoints(app =>
        {
            app.MapControllerRoute(
                name: "default",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
        });

    };
}