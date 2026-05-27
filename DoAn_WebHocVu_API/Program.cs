using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký "Người vận chuyển" DbContext vào hệ thống
builder.Services.AddDbContext<DoAnWebHocVuAdvancedContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
// Cấp phép cho Front-end (Cổng 3000) được lấy dữ liệu
builder.Services.AddCors(options =>
{
    options.AddPolicy("ChoPhepReact",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000") // Cổng của React
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // Thêm dòng này
    app.UseSwaggerUI(); // Thêm dòng này
    app.MapOpenApi();
}
app.UseCors("ChoPhepReact");
app.UseAuthorization();

app.MapControllers();

app.Run();
