var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Host.UseDefaultServiceProvider( c =>
{
    c.ValidateOnBuild = true;
});
#endif
var services = builder.Services;

services.AddServiceDiscovery();
services.ConfigureHttpClientDefaults(static http =>
{
    // Turn on service discovery by default
    http.AddServiceDiscovery();
});
services.AddMvc();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();

