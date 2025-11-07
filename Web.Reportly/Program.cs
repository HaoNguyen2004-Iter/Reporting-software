using Rotativa.AspNetCore;
using DBContext.Reportly;
using FluentEmail.Core;
using FluentEmail.MailKitSmtp;
using FluentEmail.Razor;
using MailKit.Security;
using Service.Reportly.Executes.Emails;
using Service.Reportly.Executes.Uploads;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// Đăng ký Email & Upload Services
builder.Services.AddScoped<EmailCommand>();
builder.Services.AddScoped<UploadCommand>();

// Database Context
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// SMTP Config
var smtpHost = builder.Configuration["Email:Smtp:Host"];
var smtpPort = builder.Configuration["Email:Smtp:Port"];
var smtpUser = builder.Configuration["Email:Smtp:User"];
var smtpPassword = builder.Configuration["Email:Smtp:Password"];
var fromEmail = smtpUser ?? "no-reply@localhost";

// FluentEmail
builder.Services
    .AddFluentEmail(fromEmail, "Reportly System")
    .AddRazorRenderer()
    .AddMailKitSender(new SmtpClientOptions
    {
        Server = smtpHost ?? "smtp.gmail.com",
        Port = int.TryParse(smtpPort, out var p) ? p : 587,
        User = smtpUser ?? fromEmail,
        Password = smtpPassword ?? "",
        RequiresAuthentication = !string.IsNullOrWhiteSpace(smtpPassword),
        SocketOptions = SecureSocketOptions.StartTls
    });

// HttpClient + Session
builder.Services.AddHttpClient();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// --------------------
// Middleware thứ tự chuẩn
// --------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession(); //  phải nằm sau UseRouting và trước MapControllerRoute

app.UseAuthorization();

// Cấu hình Rotativa
RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

// --------------------
// ROUTES
// --------------------

// Razor Pages (nếu bạn vẫn dùng)
app.MapRazorPages();

// Default route: vào trang Login khi mở web
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Các controller có [Route] riêng (ví dụ API)
app.MapControllers();

app.Run("http://localhost:5001");
