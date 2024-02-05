// See https://aka.ms/new-console-template for more information

using EFImplicitConversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
        services.AddTransient<IDemo, Demo>()
            .AddDbContext<ImplicitCoversionDbContext>(options =>
            {
                options.UseSqlServer(
                    "Server=localhost;Database=StackOverflow2010;Trusted_Connection=True;TrustServerCertificate=True;");
            }))
    .Build();


var demo = host.Services.GetRequiredService<IDemo>();
demo.Run();
