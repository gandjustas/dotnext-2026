[assembly: HostingStartup(typeof(RazorModule.Module))]
namespace RazorModule;

class Module : IHostingStartup, IStartupFilter
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(this);
            services.AddRazorPages().AddApplicationPart(typeof(Module).Assembly);
        });
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => 
    builder => {
        next(builder); // Не забываем вызвать следующий модуль
        
        builder.UseEndpoints(app =>
        {
            app.MapRazorPages();            
        });
    };
}
