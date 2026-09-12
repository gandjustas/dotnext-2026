
using System.Reflection;

public abstract class ModuleBase : IHostingStartup, IStartupFilter
{
    /// <summary>
    /// Секция конфигурации, в которую модули записывают себя при активации:
    /// Modules:&lt;имя сборки&gt; = полное имя типа модуля.
    /// </summary>
    public const string ModulesSection = "Modules";

    /// <summary>
    /// Сборки модулей, которые реально активировались, в порядке активации.
    /// </summary>
    /// <remarks>
    /// <c>AppDomain.CurrentDomain.GetAssemblies()</c> с фильтром по <c>HostingStartupAttribute</c>
    /// в процессе одного хоста даёт тот же ответ и в том же порядке — это проверено. Он перестаёт
    /// его давать там, где в одном процессе живёт несколько хостов, то есть в любой сборке
    /// интеграционных тестов: там загружены модули всех топологий, и каждая видит объединение.
    /// Плюс в выборку попадают чужие hosting startup — <c>Microsoft.AspNetCore.Server.IISIntegration</c>
    /// присутствует всегда. Ничего при этом не падает: модель просто собирается не та.
    /// </remarks>
    public static IReadOnlyList<Assembly> GetLoadedModules(IConfiguration configuration)
    {
        var order = (configuration[WebHostDefaults.HostingStartupAssembliesKey] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Сборка приложения загружается первой и в переменной не указана, поэтому её ранг -1.
        return [.. configuration.GetSection(ModulesSection).GetChildren()
            .OrderBy(m => Array.FindIndex(order, n => string.Equals(n, m.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(m => Assembly.Load(m.Key))];
    }

    /// <summary>
    /// Записи реестра для кода, который задаёт состав модулей явно, а не через переменную
    /// окружения — прежде всего для <c>IDesignTimeDbContextFactory</c>: иначе миграция получается
    /// для того набора модулей, который оказался в окружении, и об этом никто не сообщает.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string?>> CreateModuleRegistry(params string[] moduleAssemblyNames) =>
        from name in moduleAssemblyNames
        let attribute = Assembly.Load(name).GetCustomAttribute<HostingStartupAttribute>()
        select new KeyValuePair<string, string?>($"{ModulesSection}:{name}", attribute?.HostingStartupType.FullName);

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

    private static string RegistryKey(Assembly assembly) => $"{ModulesSection}:{assembly.GetName().Name}";

    private void ValidateReferences(IApplicationBuilder builder)
    {
        var config = builder.ApplicationServices.GetRequiredService<IConfiguration>();

        var notLoadedModulesQuery = from r in this.GetType().Assembly.GetReferencedAssemblies()
                         let a = Assembly.Load(r)
                         let hsa = a.GetCustomAttribute<HostingStartupAttribute>()
                         where hsa is not null
                         where config[RegistryKey(a)] != hsa.HostingStartupType.FullName
                         select (Assembly: a, Module: hsa.HostingStartupType);

        var notLoadedModules = notLoadedModulesQuery.ToArray();
        if (notLoadedModules.Length > 0) throw new InvalidOperationException(
            $"Referenced modules {string.Join(", ", notLoadedModules.Select(x => x.Assembly.GetName().Name))} are not initialized"
        );
    }
}
