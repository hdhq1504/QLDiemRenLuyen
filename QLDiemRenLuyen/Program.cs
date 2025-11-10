using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.Data.Student;
using QLDiemRenLuyen.Services;

var builder = WebApplication.CreateBuilder(args);

// 5.1) DI
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<StudentActivitiesRepository>();
builder.Services.AddScoped<StudentScoresRepository>();
builder.Services.AddScoped<StudentProfileRepository>();
builder.Services.AddScoped<StudentProofsRepository>();
builder.Services.AddScoped<StudentFeedbackRepository>();
builder.Services.AddScoped<StudentNotificationsRepository>();
builder.Services.AddScoped<AdminDashboardRepository>();
builder.Services.AddScoped<AdminActivitiesRepository>();
builder.Services.AddScoped<LecturerDashboardRepository>();
builder.Services.AddScoped<StaffClassesRepository>();

builder.Services.AddControllersWithViews();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

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

builder.Services.AddAuthorization();

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
            "LECTURER" => "/lecturer/dashboard",
            "ADVISOR" => "/lecturer/dashboard",
            "STAFF" => "/staff/classes/add-student",
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
    pattern: "{controller}/{action}/{id?}",
    defaults: new { controller = "Account", action = "Login" });

app.Run();
