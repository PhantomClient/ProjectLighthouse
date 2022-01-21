using LBPUnion.ProjectLighthouse.Helpers.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace LBPUnion.ProjectLighthouse.Startup;

public class TestStartup : Startup
{
    public TestStartup(IConfiguration configuration) : base(configuration)
    {}

    public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseMiddleware<FakeRemoteIPAddressMiddleware>();
        base.Configure(app, env);
    }
}