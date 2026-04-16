using Contracts;
[assembly: HostingStartup(typeof(Local.Module))]
namespace Local;

class Module : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) =>
            services.AddTransient<ICalculator, Calculator>()
        );
    }
}

class Calculator : ICalculator
{
    public Task<int> Add(int a, int b) => Task.FromResult(a + b);
}