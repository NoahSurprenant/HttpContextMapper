using HttpContextMapper;
using HttpContextMapper.Extensions;
using HttpContextMapper.Html;
using HttpContextMapper.Middlewares;
using Microsoft.Extensions.FileProviders;

namespace ExampleWebApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.ConfigureAndRegisterDefaultHttpClientWithForwardProxy();
            builder.Services.AddScoped<IContextMapper, CustomHttpContextMapper>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseMiddleware<ExceptionLoggerMiddleware>();


            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "additionalfiles")),
                RequestPath = "/additionalfiles",
            });

            app.UseHttpsRedirection();

            app.UseRouting(); // Necessary when using MapFallbackToContextMapper in conjunction with UseStaticFiles. Otherwise static files will not be reachable
            app.UseAuthorization();
            
            app.MapControllers();

            app.MapFallbackToContextMapper();
            //app.UseMiddleware<ContextMapperMiddleware>();

            app.Run();
        }
    }
}