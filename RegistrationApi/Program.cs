using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RegistrationApi.Data;
using RegistrationApi.Options;
using RegistrationApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<BcOptions>(builder.Configuration.GetSection(BcOptions.SectionName));

builder.Services.AddHttpClient<BcClient>(c => c.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var o = sp.GetRequiredService<IOptions<BcOptions>>().Value;
        // Windows auth: send the identity this process runs as.
        return new HttpClientHandler
        {
            UseDefaultCredentials = o.AuthMode.Equals("Windows", StringComparison.OrdinalIgnoreCase)
        };
    });

var app = builder.Build();

// Training setup: create the SQLite file on first run. Switch to EF migrations before production.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
