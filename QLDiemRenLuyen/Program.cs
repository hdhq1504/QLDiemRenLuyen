using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using QLDiemRenLuyen.Data;

var builder = WebApplication.CreateBuilder(args);

// 5.1) DI
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<UserRepository>();

builder.Services.AddControllersWithViews();

// 5.2) Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/AccessDenied";
        opt.SlidingExpiration = true;
        opt.ExpireTimeSpan = TimeSpan.FromDays(7);
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Strict;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization(); // sau này bạn map theo RoleName

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // chú ý thứ tự
app.UseAuthorization();

app.MapGet("/", context =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var redirectUrl = role?.ToUpperInvariant() switch
        {
            "ADMIN" => "/admin/dashboard",
            "LECTURER" => "/advisor/classes",
            "ADVISOR" => "/advisor/classes",
            _ => "/student/home"
        };
        context.Response.Redirect(redirectUrl);
    }
    else
    {
        context.Response.Redirect("/Account/Login");
    }
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
