using Modulith;


[assembly: HostingStartup(typeof(Module))]

class Module : ModuleBase
{
    protected override void Configure(IApplicationBuilder builder)
    {
        builder.UseEndpoints(app =>
        {
            app.MapGet("/host", () => "Hello from Host!");
        });
    }
}