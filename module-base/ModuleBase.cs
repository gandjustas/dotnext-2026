
using System.Reflection;

public abstract class ModuleBase : IHostingStartup, IStartupFilter
{
    public const string ModulesSection = "Modules";

    public static IReadOnlyList<Assembly> GetLoadedModules(IConfiguration configuration)
    {
        var order = (configuration[WebHostDefaults.HostingStartupAssembliesKey] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Сборка приложения загружается первой и в переменной не указана, поэтому её ранг -1.
        return [.. configuration.GetSection(ModulesSection).GetChildren()
            .OrderBy(m => Array.FindIndex(order, n => string.Equals(n, m.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(m => Assembly.Load(m.Key))];
    }

    public static IEnumerable<KeyValuePair<string, string?>> CreateModuleRegistry(params string[] moduleAssemblyNames) =>
        from name in moduleAssemblyNames
        let attribute = Assembly.Load(name).GetCustomAttribute<HostingStartupAttribute>()
        select new KeyValuePair<string, string?>($"{ModulesSection}:{name}", attribute?.HostingStartupType.FullName);

    private static string RegistryKey(Assembly assembly) => $"{ModulesSection}:{assembly.GetName().Name}";

    void IHostingStartup.Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) => {
            services.AddSingleton<IStartupFilter>(this);
            ConfigureServices(ctx, services);
        });

        builder.ConfigureAppConfiguration((ctx, configuration) => {
            var t = this.GetType();
            configuration.AddInMemoryCollection([
                new(RegistryKey(t.Assembly), t.FullName)
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
        var loadedModulesConfig = config.GetSection(ModulesSection).GetChildren();
        HashSet<string> loadelModules = [.. loadedModulesConfig.Select(c => c.Value!)];
        var referencedAssemblies = GetType().Assembly.GetReferencedAssemblies();

        var notLoadedModulesQuery = from r in referencedAssemblies
                         let a = Assembly.Load(r)
                         let hsa = a.GetCustomAttribute<HostingStartupAttribute>()
                         where hsa is not null
                         where  !loadelModules.Contains(hsa.HostingStartupType.FullName!)
                         select (Assembly: a, Module: hsa.HostingStartupType);

        var notLoadedModules = notLoadedModulesQuery.ToArray();
        if (notLoadedModules.Length > 0) throw new InvalidOperationException(
            $"Referenced modules {string.Join(", ", notLoadedModules.Select(x => x.Assembly.GetName().Name))} are not initialized"
        );
    }

    protected virtual void ConfigureServices(WebHostBuilderContext context, IServiceCollection services) { }

    protected virtual void Configure(IApplicationBuilder builder) { }

    protected virtual void ConfigureAppConfiguration(WebHostBuilderContext context, IConfigurationBuilder configuration) { }

}
