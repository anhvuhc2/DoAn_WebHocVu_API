
using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký "Người vận chuyển" DbContext vào hệ thống
builder.Services.AddDbContext<DoAnWebHocVuAdvancedContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Bỏ qua lỗi vòng lặp vô tận khi trả dữ liệu JSON
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
// Đăng ký thư viện JWT Bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        // Bắt lỗi 403 - Sai quyền (Có thẻ nhưng không đúng chức vụ)
        OnForbidden = context =>
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json; charset=utf-8";
            return context.Response.WriteAsync("{\"message\": \"Từ chối truy cập: Bạn không có quyền thực hiện chức năng này!\"}");
        },

        // Bắt lỗi 401 - Chưa đăng nhập (Không có thẻ hoặc thẻ hết hạn)
        OnChallenge = context =>
        {
            context.HandleResponse(); // Chặn cái báo lỗi mặc định của hệ thống
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json; charset=utf-8";
            return context.Response.WriteAsync("{\"message\": \"Bạn chưa đăng nhập hoặc Token đã hết hạn!\"}");
        }
    };
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập JWT Token của bạn vào ô bên dưới"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

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
    //app.MapOpenApi();
}
app.UseStaticFiles(); // Cho phép truy xuất file đính kèm từ thư mục wwwroot tĩnh
app.UseCors("ChoPhepReact");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
