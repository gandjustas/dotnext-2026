using Contracts;

[assembly: HostingStartup(typeof(RemoteGrpc.Module))]
namespace RemoteGrpc;

class Module : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) =>
            services
                .AddTransient<ICalculator, Calculator>()
                .AddGrpcClient<Grpc.Services.Calcluator.CalcluatorClient>(c => 
                    c.Address = new("http://grpc")
                )
        );
    }
}

class Calculator(Grpc.Services.Calcluator.CalcluatorClient client) : ICalculator
{
    public async Task<int> Add(int a, int b) => (await client.AddAsync(new() { 
        A = a,
        B = b
    })).Result;        
}
