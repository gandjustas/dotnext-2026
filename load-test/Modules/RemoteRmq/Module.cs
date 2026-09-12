using EasyNetQ;
using Contracts;
using Modulith;

[assembly: HostingStartup(typeof(RemoteRmq.Module))]
namespace RemoteRmq;

class Module : ModuleBase
{
    protected override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        // MOD0008: the responder is deliberately co-located with the caller here. The point of
        // this module is to measure what a round trip through RabbitMQ costs, so the handler has
        // to be in the same process — in a real deployment it would be its own topology.
#pragma warning disable MOD0008
        services
            .AddTransient<ICalculator, Calculator>()
            .AddHostedService<CalculatorBackend>()
            .AddEasyNetQ(context.Configuration.GetConnectionString("Rabbit")).UseSystemTextJson();
#pragma warning restore MOD0008
    }
}
// internal: the RPC request is this module's wire format, not a contract anybody else uses.
readonly record struct AddRequest(int A, int B);

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
