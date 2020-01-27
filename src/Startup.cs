using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using AJP.ElasticBand.API.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace AJP.ElasticBand.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddElasticBand();
            services.AddSingleton<IElasticRepository<CollectionDefinition>, CollectionDefinitionRepository<CollectionDefinition>>();
            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo 
                { 
                    Title = "ElasticBand.API", 
                    Version = "v1" ,
                    Description = "A Rest API over the ElasticBand, which makes it easy to use Elasticsearch as a simple database backend for collections of objects.",
                    Contact = new OpenApiContact 
                    {
                        Name = "Andrew Poole",
                        Email = string.Empty,
                        Url = new Uri("https://forkinthecode.net/")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "Use under MIT license",
                        Url = new Uri("https://opensource.org/licenses/MIT"),
                    }
                });

                c.DocumentFilter<AddCollectionsDocumentFilter>();

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var elasticsearchUrl = app.ApplicationServices.GetService<IConfiguration>()["elasticsearchUrl"];
            app.ApplicationServices.GetService<IElasticBand>().SetElasticsearchUrl(elasticsearchUrl);

            app.UseHttpsRedirection();

            app.UseSwagger();

            app.UseSwaggerUI(c => 
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "ElasticBand.API V1");
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
