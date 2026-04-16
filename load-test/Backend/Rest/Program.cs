using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});


var app = builder.Build();


app.MapGet("/add", ([AsParameters]AddRequest request) => request.A + request.B);

await app.RunAsync();

public readonly record struct AddRequest(int A, int B);

[JsonSerializable(typeof(AddRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
