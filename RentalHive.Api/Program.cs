using Microsoft.EntityFrameworkCore;
using RentalHive.Infrastructure; // This is the crucial using directive
using RentalHive.Infrastructure.Persistence.DatabaseContext;

var builder = WebApplication.CreateBuilder(args);


// including the DbContext, IUserRepository, IPasswordHasher, etc.
builder.Services.AddInfrastructureServices(builder.Configuration);
// --- FIX ENDS HERE ---


builder.Services.AddControllers();

// Add API Documentation (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "RentalHive API", Version = "v1" });
});

// Add CORS policy to allow your Blazor app to communicate with the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
});


var app = builder.Build();

// Ensure database is created and migrations are applied at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RentalHiveDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Use Swagger UI in development mode
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RentalHive API v1"));
}

app.UseHttpsRedirection();

// Use the CORS policy
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
