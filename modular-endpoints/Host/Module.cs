
[assembly: HostingStartup(typeof(Module))]

class Module : IHostingStartup, IStartupFilter
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(this);
        });
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
    (builder) =>
    {
        next(builder); // Не забываем вызвать следующий модуль
        
        builder.UseEndpoints(app =>
        {
            app.MapGet("/host", () => "Hello from Host!");
        });
    };
}