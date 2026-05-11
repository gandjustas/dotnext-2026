
using System.Reflection;

public abstract class ModuleBase : IHostingStartup, IStartupFilter
{
    protected virtual void ConfigureServices(WebHostBuilderContext context, IServiceCollection services) { }

    protected virtual void Configure(IApplicationBuilder builder) { }

    protected virtual void ConfigureAppConfiguration(WebHostBuilderContext context, IConfigurationBuilder configuration)
    {
    }

    void IHostingStartup.Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) => {
            services.AddSingleton<IStartupFilter>(this);
            ConfigureServices(ctx, services);
        });

        builder.ConfigureAppConfiguration((ctx, configuration) => {
            var t = this.GetType();
            configuration.AddInMemoryCollection([
                new(t.Assembly.FullName!, t.FullName)
            ]);
            ConfigureAppConfiguration(ctx, configuration);
        });
    }

    Action<IApplicationBuilder> IStartupFilter.Configure(Action<IApplicationBuilder> next) => builder => { 
        ValidateReferences(builder); 
        next(builder); 
        Configure(builder); 
    };

    private void ValidateReferences(IApplicationBuilder builder)
    {
        var config = builder.ApplicationServices.GetRequiredService<IConfiguration>();

        var notLoadedModulesQuery = from r in this.GetType().Assembly.GetReferencedAssemblies()
                         let a = Assembly.Load(r)
                         let hsa = a.GetCustomAttribute<HostingStartupAttribute>()
                         where hsa is not null
                         where config[a.FullName!] != hsa.HostingStartupType.FullName
                         select (Assembly: a, Module: hsa.HostingStartupType);

        var notLoadedModules = notLoadedModulesQuery.ToArray();
        if (notLoadedModules.Length > 0) throw new InvalidOperationException(
            $"Referenced modules {string.Join(", ", notLoadedModules.Select(x => x.Assembly.GetName().Name))} are not initialized"
        );
    }
}
