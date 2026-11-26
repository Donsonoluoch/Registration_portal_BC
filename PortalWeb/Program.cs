using PortalWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient<RegistrationApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RegistrationApi:BaseUrl"]
        ?? throw new InvalidOperationException("RegistrationApi:BaseUrl is not configured."));
    client.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["RegistrationApi:ApiKey"] ?? "");
    client.Timeout = TimeSpan.FromSeconds(40);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
