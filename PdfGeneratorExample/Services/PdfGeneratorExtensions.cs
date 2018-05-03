using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PdfGeneratorExample
{
    public static class PdfGeneratorExtensions
    {
        public static void AddPdfGenerator(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<PdfGeneratorConfig>(config.GetSection("PdfGeneratorConfig"));
            services.AddTransient<IPdfGenerator, PdfGenerator>();
        }
    }
}
