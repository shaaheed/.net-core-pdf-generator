using Microsoft.Extensions.Options;
using Msi.WkHtmlToPdf;
using System.Runtime.InteropServices;

namespace PdfGeneratorExample
{
    public class PdfGenerator : IPdfGenerator
    {

        private readonly PdfGeneratorConfig _pdfGeneratorConfig;

        public PdfGenerator(IOptionsMonitor<PdfGeneratorConfig> monitor)
        {
            _pdfGeneratorConfig = monitor.CurrentValue;
        }

        public byte[] Generate(string htmlContent)
        {
            var pdfGenerator = new WkHtmlToPdfGenerator();
            (string exeName, string exePath) = GetPdfToolConfig();
            pdfGenerator.WkHtmlToPdfExeName = exeName;
            pdfGenerator.PdfToolPath = exePath;
            return pdfGenerator.GeneratePdf(htmlContent);
        }

        private (string, string) GetPdfToolConfig()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return (_pdfGeneratorConfig.LinuxExeName, _pdfGeneratorConfig.WindowsExePath);
            }
            else
            {
                return (_pdfGeneratorConfig.WindowsExeName, _pdfGeneratorConfig.WindowsExePath);
            }
        }
    }
}
