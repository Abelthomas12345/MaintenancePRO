using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using MaintenancePRO.Api.Data;
using MaintenancePRO.Api.Models;
using MaintenancePRO.Api.Security;
using MaintenancePRO.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var b = WebApplication.CreateBuilder(args);

// ---------- JSON : énumérations en texte ("High"), cycles ignorés par sécurité (les contrôleurs renvoient des DTO) ----------
b.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
b.Services.AddEndpointsApiExplorer();
b.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "MaintenancePRO API", Version = "v1", Description = "Saisir uniquement le jeton (sans « Bearer ») dans Authorize." });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Description = "Jeton JWT sans préfixe Bearer.", Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});

b.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(b.Configuration.GetConnectionString("Default")));
b.Services.AddHttpContextAccessor();

b.Services.AddIdentityCore<ApplicationUser>(o =>
{
    o.Password.RequiredLength = 12; o.Password.RequireDigit = true; o.Password.RequireLowercase = true; o.Password.RequireUppercase = true; o.Password.RequireNonAlphanumeric = true;
    o.User.RequireUniqueEmail = true;
    o.Lockout.AllowedForNewUsers = true; o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();

var jwtKey = b.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Clé JWT absente ou trop courte. Configure Jwt:Key via User Secrets ou variable d'environnement (32 octets minimum).");

b.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false; // les claims gardent leurs noms courts : sub, name, role, sstamp
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = b.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = b.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "name", RoleClaimType = "role"
    };
    o.Events = new JwtBearerEvents
    {
        // Statut et rôle revérifiés à CHAQUE requête : compte suspendu, rôle modifié ou mot de passe changé => jeton refusé immédiatement.
        OnTokenValidated = async ctx =>
        {
            var um = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var id = ctx.Principal?.FindFirst("sub")?.Value;
            var stamp = ctx.Principal?.FindFirst("sstamp")?.Value;
            var u = id == null ? null : await um.FindByIdAsync(id);
            if (u == null || u.Status != AccountStatus.Active || u.SecurityStamp != stamp) ctx.Fail("Session invalide ou révoquée.");
        },
        OnAuthenticationFailed = ctx =>
        {
            ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Jwt").LogWarning("JWT refusé : {Reason}", ctx.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

b.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.RequireRole(AppRoles.Admin));
    o.AddPolicy("ManageEquipment", p => p.RequireRole(AppRoles.Admin, AppRoles.Supervisor));
    o.AddPolicy("ManageMaintenance", p => p.RequireRole(AppRoles.Admin, AppRoles.Supervisor, AppRoles.Technician));
});

b.Services.AddScoped<TokenService>();
b.Services.AddScoped<AuditService>();
b.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});

var app = b.Build();

// ---------- Initialisation base, rôles, admin, données de démo ----------
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    if (app.Configuration.GetValue<bool>("Database:UseMigrations")) await db.Database.MigrateAsync();
    else await db.Database.EnsureCreatedAsync();
    try { _ = await db.RefreshTokens.AnyAsync(); _ = await db.Breakdowns.Select(x => x.AssignedTechnicianId).FirstOrDefaultAsync(); }
    catch (Exception ex)
    {
        throw new InvalidOperationException("Le schéma de la base est obsolète (EnsureCreated ne met pas à jour une base existante). Arrête l'API, supprime maintenancepro.db puis relance. Pour un vrai suivi du schéma, utilise les migrations EF Core (voir README).", ex);
    }

    var rm = sp.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in AppRoles.All) if (!await rm.RoleExistsAsync(role)) await rm.CreateAsync(new IdentityRole(role));

    var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
    var email = app.Configuration["BootstrapAdmin:Email"]; var password = app.Configuration["BootstrapAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password) && await um.FindByEmailAsync(email) == null)
    {
        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Administrateur", Status = AccountStatus.Active };
        var cr = await um.CreateAsync(admin, password);
        if (cr.Succeeded) await um.AddToRoleAsync(admin, AppRoles.Admin);
        else app.Logger.LogError("Création admin impossible : {Errors}", string.Join(",", cr.Errors.Select(x => x.Code)));
    }

    // Démo : uniquement en développement et si Seed:DemoData=true (équipements) ; comptes si Seed:DemoPassword est défini.
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:DemoData"))
    {
        if (!await db.Equipments.AnyAsync())
        {
            db.Equipments.AddRange(
                new Equipment { Code = "EQ-001", Name = "Compresseur principal", Location = "Atelier A", Category = "Compresseur", Manufacturer = "Atlas Copco" },
                new Equipment { Code = "EQ-002", Name = "Pompe hydraulique", Location = "Atelier B", Category = "Pompe" },
                new Equipment { Code = "EQ-003", Name = "Convoyeur industriel", Location = "Ligne 2", Category = "Convoyeur" });
            await db.SaveChangesAsync();
        }
        var demoPwd = app.Configuration["Seed:DemoPassword"];
        if (!string.IsNullOrWhiteSpace(demoPwd))
            foreach (var (mail, name, role) in new[] { ("superviseur@maintenancepro.local", "Samuel Mbarga", AppRoles.Supervisor), ("technicien@maintenancepro.local", "Marc Tchoumi", AppRoles.Technician), ("user@maintenancepro.local", "Utilisateur Démo", AppRoles.User) })
                if (await um.FindByEmailAsync(mail) == null)
                {
                    var u = new ApplicationUser { UserName = mail, Email = mail, EmailConfirmed = true, DisplayName = name, Status = AccountStatus.Active };
                    if ((await um.CreateAsync(u, demoPwd)).Succeeded) await um.AddToRoleAsync(u, role);
                }
    }
}

// ---------- Pipeline ----------
app.UseExceptionHandler(a => a.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { message = "Erreur interne du serveur." });
}));
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
else { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();
app.Run();
