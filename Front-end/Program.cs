using Front_end.Servicios.Api;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<JwtDelegatingHandler>();
builder.Services.AddHttpClient<OasisApiClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7001/"))
    .AddHttpMessageHandler<JwtDelegatingHandler>();
builder.Services.AddHttpClient<HabitacionesApi>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7001/"))
    .AddHttpMessageHandler<JwtDelegatingHandler>();
builder.Services.AddHttpClient<DistribucionApi>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7001/"))
    .AddHttpMessageHandler<JwtDelegatingHandler>();
builder.Services.AddHttpClient<ReservasApi>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7001/"))
    .AddHttpMessageHandler<JwtDelegatingHandler>();
builder.Services.AddHttpClient<HistorialApi>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7001/"))
    .AddHttpMessageHandler<JwtDelegatingHandler>();
builder.Services.AddHttpClient<ReportesApi>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7001/"))
    .AddHttpMessageHandler<JwtDelegatingHandler>();
builder.Services.AddHttpClient<FacturacionApi>(c =>
        c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7001/"))
    .AddHttpMessageHandler<JwtDelegatingHandler>();

// El JWT de la API viaja en la cookie HttpOnly; sin sesión o con token vencido se vuelve al login [D-02].
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "OasisStay.Sesion";
        o.Cookie.HttpOnly = true;
        o.LoginPath = "/";
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            // Rol sin permiso a la pantalla: se vuelve a su inicio con aviso [RNF-06][E2 §6].
            var inicio = ctx.HttpContext.User.IsInRole("AmaDeLlaves") ? "/Ventanas/Habitaciones" : "/Ventanas/PanelControl";
            ctx.Response.Redirect(inicio + "?denegado=1");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
