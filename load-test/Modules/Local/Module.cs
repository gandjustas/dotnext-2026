using Contracts;
using Modulith;
[assembly: HostingStartup(typeof(Local.Module))]
namespace Local;

class Module : ModuleBase
{
    protected override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services.AddTransient<ICalculator, Calculator>();
    }
}

class Calculator : ICalculator
{
    public Task<int> Add(int a, int b) => Task.FromResult(a + b);
}