using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OasisStay.Aplicacion;
using OasisStay.Infraestructura;
using OasisStay.Infraestructura.Persistencia;
using OasisStay.Infraestructura.Semilla;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.Configure<PoliticasOpciones>(builder.Configuration.GetSection("Politicas"));
builder.Services.AddInfraestructura(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((o, cfg) =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = cfg["Jwt:Emisor"],
            ValidAudience = cfg["Jwt:Audiencia"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Clave"]
                ?? throw new InvalidOperationException("Falta Jwt:Clave (user-secrets o variable de entorno) [D-02]."))),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? new[] { "https://localhost:5001" })
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OasisStayDbContext>();
    if (db.Database.IsRelational()) await db.Database.MigrateAsync();
    else await db.Database.EnsureCreatedAsync();
    await SemillaInicial.EjecutarAsync(scope.ServiceProvider);

    // [D-39] --semilla-demo: solo Development, idempotente
    if (args.Contains("--semilla-demo") && app.Environment.IsDevelopment())
        await scope.ServiceProvider.GetRequiredService<EjecutorSemillaDemo>().EjecutarAsync(db);
}

app.Run();

public partial class Program { }
