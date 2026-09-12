using Contracts;
using FusionModules;

[assembly: HostingStartup(typeof(RemoteRest.Module))]
namespace RemoteRest;

class Module : ModuleBase
{
    protected override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    { 
        services.AddHttpClient<ICalculator, Calculator>(c => c.BaseAddress = new ("http://rest"));
    }
}

class Calculator(HttpClient client) : ICalculator
{
    public async Task<int> Add(int a, int b) => int.Parse(await client.GetByteArrayAsync($"/add?a={a}&b={b}"));
}
