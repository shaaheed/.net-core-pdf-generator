using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace PdfGeneratorExample
{
    [Route("api")]
    public class PdfController : Controller
    {

        private readonly IHostingEnvironment _env;
        private readonly IPdfGenerator _pdfGenerator;

        public PdfController(
            IHostingEnvironment env,
            IPdfGenerator pdfGenerator)
        {
            _env = env;
            _pdfGenerator = pdfGenerator;
        }

        // POST
        [HttpPost("pdf")]
        public IActionResult Post()
        {
            string templatePath = Path.Combine(_env.ContentRootPath, "Templates/template.cshtml");
            if (System.IO.File.Exists(templatePath))
            {
                string content = System.IO.File.ReadAllText(templatePath);
                byte[] pdf = _pdfGenerator.Generate(content);
                if (pdf != null && pdf.Length > 0)
                {
                    string contetntType = @"application/pdf";
                    HttpContext.Response.ContentType = contetntType;
                    var result = new FileContentResult(pdf, contetntType);
                    result.FileDownloadName = "template.pdf";
                    return result;
                }
            }
            return NoContent();
        }
    }
}
