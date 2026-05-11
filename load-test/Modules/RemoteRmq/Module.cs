using EasyNetQ;
using Contracts;

[assembly: HostingStartup(typeof(RemoteRmq.Module))]
namespace RemoteRmq;

class Module : ModuleBase
{
    protected override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services
            .AddTransient<ICalculator, Calculator>()
            .AddHostedService<CalculatorBackend>()
            .AddEasyNetQ(context.Configuration.GetConnectionString("Rabbit")).UseSystemTextJson();
    }
}
public readonly record struct AddRequest(int A, int B);

class Calculator(IRpc rpc) : ICalculator
{
    public Task<int> Add(int a, int b) => 
        rpc.RequestAsync<AddRequest, int>(new (a, b), 
            c => c.WithExpiration(TimeSpan.FromSeconds(60)));
}

internal class CalculatorBackend(IRpc rpc) : IHostedService
{
    IAsyncDisposable? subscription = null;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        subscription = await rpc.RespondAsync<AddRequest, int>(
            x => x.A + x.B, cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await subscription!.DisposeAsync();
    }
}
