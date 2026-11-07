var builder = WebApplication.CreateBuilder(args).ValidateModules();

builder.Services.AddOpenApi();
var app = builder.Build();
app.UseRouting();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapStaticAssets();
await app.RunAsync();
