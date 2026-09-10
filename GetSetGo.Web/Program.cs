using System.Text.Json.Serialization;
using GetSetGo.Web.Data;
using GetSetGo.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GetSetGo API",
        Version = "v1",
        Description = "Log in using /api/v1/auth/login or the website in the other tab. " +
            "Requests use your browser session cookie. Swagger automatically fetches a CSRF token " +
            "for POST/PUT requests. Client and Admin endpoints require the corresponding account role."
    });
    options.DocInclusionPredicate((_, description) =>
        description.RelativePath?.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase) == true);
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else
                context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
            else
                context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAppRepository, AppRepository>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITradingService, TradingService>();
builder.Services.AddScoped<DatabaseInitializer>();

var app = builder.Build();

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "GetSetGo API v1");
        options.DocumentTitle = "GetSetGo API - Swagger";
        options.UseRequestInterceptor("""
            function (request) {
                const url = new URL(request.url, window.location.href);
                if (url.origin !== window.location.origin) return request;
                request.credentials = 'same-origin';
                const method = (request.method || 'GET').toUpperCase();
                if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(method) &&
                    !url.pathname.endsWith('/auth/login')) {
                    const apiRoot = url.pathname.indexOf('/api/v1/');
                    if (apiRoot < 0) return request;
                    return fetch(
                        url.pathname.slice(0, apiRoot) + '/api/v1/security/antiforgery',
                        { credentials: 'same-origin', cache: 'no-store' })
                        .then(function (response) {
                            if (!response.ok) throw new Error('Unable to retrieve the CSRF token. Log in and try again.');
                            return response.json();
                        })
                        .then(function (tokens) {
                            request.headers = request.headers || {};
                            request.headers[tokens.headerName] = tokens.requestToken;
                            return request;
                        });
                }
                return request;
            }
            """.ReplaceLineEndings(" ")); // Embedded JS requires one line, single-quoted strings and a regular function.
    });
    DevelopmentBrowserLauncher.Register(app);
//}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
