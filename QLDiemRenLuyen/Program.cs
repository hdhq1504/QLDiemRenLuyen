using Microsoft.AspNetCore.Authentication.Cookies;
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
