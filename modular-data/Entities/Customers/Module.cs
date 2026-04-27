[assembly:HostingStartup(typeof(Module))]

class Module : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
    }
}