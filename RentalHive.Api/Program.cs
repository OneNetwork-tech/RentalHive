using RentalHive.Infrastructure; // For AddInfrastructureServices

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// 1. Register services from our Infrastructure layer (DbContext, Repositories)
//builder.Services.AddInfrastructureServices(builder.Configuration);


builder.Services.AddControllers();

// 2. Add API Documentation (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "RentalHive API", Version = "v1" });
});

// 3. Add CORS policy to allow our Blazor app to communicate with the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
});


var app = builder.Build();

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

// In a real app, you would add authentication middleware here
// app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
