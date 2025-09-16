using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using MMLib.SwaggerForOcelot;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Add API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();  // ✅ important for SwaggerForOcelot

builder.Services.AddOcelot();
builder.Services.AddSwaggerForOcelot(builder.Configuration);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// ✅ Only bind to Render’s assigned PORT (via Dockerfile ENV)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://+:{port}");

// ✅ Don't add https://+:443 (Render handles TLS externally)

// Swagger only in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerForOcelotUI(opt =>
    {
        opt.PathToSwaggerGenerator = "/swagger/docs";
    });
}

// ❌ Remove HTTPS redirection (causes crashes in Render)
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

await app.UseOcelot();

app.Run();
