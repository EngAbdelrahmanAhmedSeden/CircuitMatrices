using CircuitMatrices.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC: controllers + Razor views
builder.Services.AddControllersWithViews();

// The analysis engine is stateless, so one instance is enough.
builder.Services.AddSingleton<IGraphAnalyzerService, GraphAnalyzerService>();

var app = builder.Build();

app.UseStaticFiles();   // wwwroot/css, wwwroot/js
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
