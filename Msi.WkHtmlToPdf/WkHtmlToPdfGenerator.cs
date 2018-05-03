using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Msi.WkHtmlToPdf
{
    /// <summary>
    /// Html to PDF converter component (C# WkHtmlToPdf process wrapper).
    /// </summary>
    public class WkHtmlToPdfGenerator
    {
        private class PdfSettings
        {
            public string CoverFilePath;

            public string HeaderFilePath;

            public string FooterFilePath;

            public WkHtmlInput[] InputFiles;

            public string OutputFile;
        }

        private Process WkHtmlToPdfProcess;

        private bool batchMode;

        private const string headerFooterHtmlTpl = "<!DOCTYPE html><html><head>\r\n<meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\" />\r\n<script>\r\nfunction subst() {{\r\n    var vars={{}};\r\n    var x=document.location.search.substring(1).split('&');\r\n\r\n    for(var i in x) {{var z=x[i].split('=',2);vars[z[0]] = unescape(z[1]);}}\r\n    var x=['frompage','topage','page','webpage','section','subsection','subsubsection'];\r\n    for(var i in x) {{\r\n      var y = document.getElementsByClassName(x[i]);\r\n      for(var j=0; j<y.length; ++j) y[j].textContent = vars[x[i]];\r\n    }}\r\n}}\r\n</script></head><body style=\"border:0; margin: 0;\" onload=\"subst()\">{0}</body></html>\r\n";

        private static object globalObj = new object();

        private static string[] ignoreWkHtmlToPdfErrLines = new string[6]
        {
            "Exit with code 1 due to network error: ContentNotFoundError",
            "QFont::setPixelSize: Pixel size <= 0",
            "Exit with code 1 due to network error: ProtocolUnknownError",
            "Exit with code 1 due to network error: HostNotFoundError",
            "Exit with code 1 due to network error: ContentOperationNotPermittedError",
            "Exit with code 1 due to network error: UnknownContentError"
        };

        /// <summary>
        /// Get or set path where WkHtmlToPdf tool is located
        /// </summary>
        /// <remarks>
        /// By default this property points to the folder where application assemblies are located.
        /// If WkHtmlToPdf tool files are not present PdfConverter expands them from DLL resources.
        /// </remarks>
        public string PdfToolPath { get; set; }

        /// <summary>
        /// Get or set WkHtmlToPdf tool EXE file name ('wkhtmltopdf.exe' by default)
        /// </summary>
        public string WkHtmlToPdfExeName { get; set; }

        /// <summary>
        /// Get or set location for temp files (if not specified location returned by <see cref="M:System.IO.Path.GetTempPath" /> is used for temp files)
        /// </summary>
        /// <remarks>Temp files are used for providing cover page/header/footer HTML templates to wkhtmltopdf tool.</remarks>
        public string TempFilesPath { get; set; }

        /// <summary>
        /// Get or set PDF page orientation
        /// </summary>
        public PageOrientation Orientation {  get; set; }

        /// <summary>
        /// Get or set PDF page orientation
        /// </summary>
        public PageSize Size { get; set; }

        /// <summary>
        /// Gets or sets option to generate low quality PDF (shrink the result document space)
        /// </summary>
        public bool LowQuality { get; set; }

        /// <summary>
        /// Gets or sets option to generate grayscale PDF
        /// </summary>
        public bool Grayscale { get; set; }

        /// <summary>
        /// Gets or sets zoom factor
        /// </summary>
        public float Zoom { get; set; }

        /// <summary>
        /// Gets or sets PDF page margins (in mm)
        /// </summary>
        public PageMargins Margins { get; set; }

        /// <summary>
        /// Gets or sets PDF page width (in mm)
        /// </summary>
        public float? PageWidth { get; set; }

        /// <summary>
        /// Gets or sets PDF page height (in mm)
        /// </summary>
        public float? PageHeight { get; set; }

        /// <summary>
        /// Gets or sets TOC generation flag
        /// </summary>
        public bool GenerateToc { get; set; }

        /// <summary>
        /// Gets or sets custom TOC header text (default: "Table of Contents")
        /// </summary>
        public string TocHeaderText { get; set; }

        /// <summary>
        /// Custom WkHtmlToPdf global options
        /// </summary>
        public string CustomWkHtmlArgs { get; set; }

        /// <summary>
        /// Custom WkHtmlToPdf page options
        /// </summary>
        public string CustomWkHtmlPageArgs { get; set; }

        /// <summary>
        /// Custom WkHtmlToPdf cover options (applied only if cover content is specified)
        /// </summary>
        public string CustomWkHtmlCoverArgs { get; set; }

        /// <summary>
        /// Custom WkHtmlToPdf toc options (applied only if GenerateToc is true)
        /// </summary>
        public string CustomWkHtmlTocArgs { get; set; }

        /// <summary>
        /// Get or set custom page header HTML
        /// </summary>
        public string PageHeaderHtml { get; set; }

        /// <summary>
        /// Get or set custom page footer HTML
        /// </summary>
        public string PageFooterHtml { get; set; }

        /// <summary>
        /// Get or set maximum execution time for PDF generation process (by default is null that means no timeout)
        /// </summary>
        public TimeSpan? ExecutionTimeout { get; set; }

        /// <summary>
        /// Suppress wkhtmltopdf debug/info log messages (by default is true)
        /// </summary>
        public bool Quiet { get; set; }

        /// <summary>
        /// Occurs when log line is received from WkHtmlToPdf process
        /// </summary>
        /// <remarks>
        /// Quiet mode should be disabled if you want to get wkhtmltopdf info/debug messages
        /// </remarks>
        public event EventHandler<DataReceivedEventArgs> LogReceived;

        /// <summary>
        /// Create new instance of HtmlToPdfConverter
        /// </summary>
        public WkHtmlToPdfGenerator()
        {
            string text2 = PdfToolPath = null;
            TempFilesPath = null;
            WkHtmlToPdfExeName = "wkhtmltopdf.exe";
            Orientation = PageOrientation.Default;
            Size = PageSize.Default;
            LowQuality = false;
            Grayscale = false;
            Quiet = true;
            Zoom = 1f;
            Margins = new PageMargins();
        }

        /// <summary>
        /// Generates PDF by specifed HTML content
        /// </summary>
        /// <param name="htmlContent">HTML content</param>
        /// <returns>PDF bytes</returns>
        public byte[] GeneratePdf(string htmlContent)
        {
            return GeneratePdf(htmlContent, null);
        }

        /// <summary>
        /// Generates PDF by specfied HTML content and prepend cover page (useful with GenerateToc option)
        /// </summary>
        /// <param name="htmlContent">HTML document</param>
        /// <param name="coverHtml">first page HTML (optional; can be null)</param>
        /// <returns>PDF bytes</returns>
        public byte[] GeneratePdf(string htmlContent, string coverHtml)
        {
            MemoryStream memoryStream = new MemoryStream();
            GeneratePdf(htmlContent, coverHtml, memoryStream);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Generates PDF by specfied HTML content (optionally with the cover page).
        /// </summary>
        /// <param name="htmlContent">HTML document</param>
        /// <param name="coverHtml">first page HTML (optional; can be null)</param>
        /// <param name="output">output stream for generated PDF</param>
        public void GeneratePdf(string htmlContent, string coverHtml, Stream output)
        {
            if (htmlContent == null)
            {
                throw new ArgumentNullException("htmlContent");
            }
            GeneratePdfInternal(null, htmlContent, coverHtml, "-", output);
        }

        /// <summary>
        /// Generates PDF by specfied HTML content (optionally with the cover page).
        /// </summary>
        /// <param name="htmlContent">HTML document</param>
        /// <param name="coverHtml">first page HTML (can be null)</param>
        /// <param name="outputPdfFilePath">path to the output PDF file (if file already exists it will be removed before PDF generation)</param>
        public void GeneratePdf(string htmlContent, string coverHtml, string outputPdfFilePath)
        {
            if (htmlContent == null)
            {
                throw new ArgumentNullException("htmlContent");
            }
            GeneratePdfInternal(null, htmlContent, coverHtml, outputPdfFilePath, null);
        }

        /// <summary>
        /// Generate PDF by specfied HTML content and prepend cover page (useful with GenerateToc option)
        /// </summary>
        /// <param name="htmlFilePath">path to HTML file or absolute URL</param>
        /// <param name="coverHtml">first page HTML (optional, can be null)</param>
        /// <returns>PDF bytes</returns>
        public byte[] GeneratePdfFromFile(string htmlFilePath, string coverHtml)
        {
            MemoryStream memoryStream = new MemoryStream();
            GeneratePdfInternal(new string[1]
            {
                htmlFilePath
            }, coverHtml, memoryStream);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Generate PDF by specfied HTML content and prepend cover page (useful with GenerateToc option)
        /// </summary>
        /// <param name="htmlFilePath">path to HTML file or absolute URL</param>
        /// <param name="coverHtml">first page HTML (optional, can be null)</param>
        /// <param name="output">output stream for generated PDF</param>
        public void GeneratePdfFromFile(string htmlFilePath, string coverHtml, Stream output)
        {
            GeneratePdfInternal(new string[1]
            {
                htmlFilePath
            }, coverHtml, output);
        }

        /// <summary>
        /// Generate PDF by specfied HTML content and prepend cover page (useful with GenerateToc option)
        /// </summary>
        /// <param name="htmlFilePath">path to HTML file or absolute URL</param>
        /// <param name="coverHtml">first page HTML (optional, can be null)</param>
        /// <param name="outputPdfFilePath">path to the output PDF file (if file already exists it will be removed before PDF generation)</param>
        public void GeneratePdfFromFile(string htmlFilePath, string coverHtml, string outputPdfFilePath)
        {
            if (File.Exists(outputPdfFilePath))
            {
                File.Delete(outputPdfFilePath);
            }
            GeneratePdfInternal(new WkHtmlInput[1]
            {
                new WkHtmlInput(htmlFilePath)
            }, null, coverHtml, outputPdfFilePath, null);
        }

        /// <summary>
        /// Generate PDF into specified <see cref="T:System.IO.Stream" /> by several HTML documents (local files or URLs) 
        /// </summary>
        /// <param name="htmlFiles">list of HTML files or URLs</param>
        /// <param name="coverHtml">first page HTML (optional, can be null)</param>
        /// <param name="output">output stream for generated PDF</param>
        public void GeneratePdfFromFiles(string[] htmlFiles, string coverHtml, Stream output)
        {
            GeneratePdfInternal(htmlFiles, coverHtml, output);
        }

        private WkHtmlInput[] GetWkHtmlInputFromFiles(string[] files)
        {
            WkHtmlInput[] array = new WkHtmlInput[files.Length];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new WkHtmlInput(files[i]);
            }
            return array;
        }

        /// <summary>
        /// Generate PDF into specified output file by several HTML documents (local files or URLs) 
        /// </summary>
        /// <param name="htmlFiles">list of HTML files or URLs</param>
        /// <param name="coverHtml">first page HTML (optional, can be null)</param>
        /// <param name="outputPdfFilePath">path to output PDF file (if file already exists it will be removed before PDF generation)</param>
        public void GeneratePdfFromFiles(string[] htmlFiles, string coverHtml, string outputPdfFilePath)
        {
            GeneratePdfFromFiles(GetWkHtmlInputFromFiles(htmlFiles), coverHtml, outputPdfFilePath);
        }

        /// <summary>
        /// Generate PDF into specified output file by several HTML documents (local files or URLs) 
        /// </summary>
        /// <param name="htmlFiles">list of <see cref="T:NReco.PdfGenerator.WkHtmlInput" /></param>
        /// <param name="coverHtml">first page HTML (optional, can be null)</param>
        /// <param name="outputPdfFilePath">path to output PDF file (if file already exists it will be removed before PDF generation)</param>
        public void GeneratePdfFromFiles(WkHtmlInput[] inputs, string coverHtml, string outputPdfFilePath)
        {
            if (File.Exists(outputPdfFilePath))
            {
                File.Delete(outputPdfFilePath);
            }
            GeneratePdfInternal(inputs, null, coverHtml, outputPdfFilePath, null);
        }

        private void GeneratePdfInternal(string[] htmlFiles, string coverHtml, Stream output)
        {
            GeneratePdfInternal(GetWkHtmlInputFromFiles(htmlFiles), null, coverHtml, "-", output);
        }

        private void CheckWkHtmlProcess()
        {
            if (batchMode) { return; }
            if (WkHtmlToPdfProcess == null) { return; }
            throw new InvalidOperationException("WkHtmlToPdf process is already started");
        }

        private string GetTempPath()
        {
            if (!string.IsNullOrEmpty(TempFilesPath) && !Directory.Exists(TempFilesPath))
            {
                Directory.CreateDirectory(TempFilesPath);
            }
            return TempFilesPath ?? Path.GetTempPath();
        }

        private string GetToolExePath()
        {
            if (string.IsNullOrEmpty(PdfToolPath))
            {
                throw new ArgumentException("PdfToolPath property is not initialized with path to wkhtmltopdf binaries");
            }
            string text = Path.Combine(PdfToolPath, WkHtmlToPdfExeName);
            if (!File.Exists(text))
            {
                throw new FileNotFoundException("Cannot find wkhtmltopdf executable: " + text);
            }
            return text;
        }

        private string CreateTempFile(string content, string tempPath, List<string> tempFilesList)
        {
            string text = Path.Combine(tempPath, "pdfgen-" + Path.GetRandomFileName() + ".html");
            tempFilesList.Add(text);
            if (content != null)
            {
                File.WriteAllBytes(text, Encoding.UTF8.GetBytes(content));
            }
            return text;
        }

        private string ComposeArgs(PdfSettings pdfSettings)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (Quiet)
            {
                stringBuilder.Append(" -q ");
            }
            if (Orientation != 0)
            {
                stringBuilder.AppendFormat(" -O {0} ", Orientation.ToString());
            }
            if (Size != 0)
            {
                stringBuilder.AppendFormat(" -s {0} ", Size.ToString());
            }
            if (LowQuality)
            {
                stringBuilder.Append(" -l ");
            }
            if (Grayscale)
            {
                stringBuilder.Append(" -g ");
            }
            if (Margins != null)
            {
                if (Margins.Top.HasValue)
                {
                    stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " -T {0}", Margins.Top);
                }
                if (Margins.Bottom.HasValue)
                {
                    stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " -B {0}", Margins.Bottom);
                }
                if (Margins.Left.HasValue)
                {
                    stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " -L {0}", Margins.Left);
                }
                if (Margins.Right.HasValue)
                {
                    stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " -R {0}", Margins.Right);
                }
            }
            if (PageWidth.HasValue)
            {
                stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " --page-width {0}", PageWidth);
            }
            if (PageHeight.HasValue)
            {
                stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " --page-height {0}", PageHeight);
            }
            if (pdfSettings.HeaderFilePath != null)
            {
                stringBuilder.AppendFormat(" --header-html \"{0}\"", pdfSettings.HeaderFilePath);
            }
            if (pdfSettings.FooterFilePath != null)
            {
                stringBuilder.AppendFormat(" --footer-html \"{0}\"", pdfSettings.FooterFilePath);
            }
            if (!string.IsNullOrEmpty(CustomWkHtmlArgs))
            {
                stringBuilder.AppendFormat(" {0} ", CustomWkHtmlArgs);
            }
            if (pdfSettings.CoverFilePath != null)
            {
                stringBuilder.AppendFormat(" cover \"{0}\" ", pdfSettings.CoverFilePath);
                if (!string.IsNullOrEmpty(CustomWkHtmlCoverArgs))
                {
                    stringBuilder.AppendFormat(" {0} ", CustomWkHtmlCoverArgs);
                }
            }
            if (GenerateToc)
            {
                stringBuilder.Append(" toc ");
                if (!string.IsNullOrEmpty(TocHeaderText))
                {
                    stringBuilder.AppendFormat(" --toc-header-text \"{0}\"", TocHeaderText.Replace("\"", "\\\""));
                }
                if (!string.IsNullOrEmpty(CustomWkHtmlTocArgs))
                {
                    stringBuilder.AppendFormat(" {0} ", CustomWkHtmlTocArgs);
                }
            }
            WkHtmlInput[] inputFiles = pdfSettings.InputFiles;
            foreach (WkHtmlInput wkHtmlInput in inputFiles)
            {
                stringBuilder.AppendFormat(" \"{0}\" ", wkHtmlInput.Input);
                string text = wkHtmlInput.CustomWkHtmlPageArgs ?? CustomWkHtmlPageArgs;
                if (!string.IsNullOrEmpty(text))
                {
                    stringBuilder.AppendFormat(" {0} ", text);
                }
                if (wkHtmlInput.HeaderFilePath != null)
                {
                    stringBuilder.AppendFormat(" --header-html \"{0}\"", wkHtmlInput.HeaderFilePath);
                }
                if (wkHtmlInput.FooterFilePath != null)
                {
                    stringBuilder.AppendFormat(" --footer-html \"{0}\"", wkHtmlInput.FooterFilePath);
                }
                if (Zoom != 1f)
                {
                    stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " --zoom {0} ", Zoom);
                }
            }
            stringBuilder.AppendFormat(" \"{0}\" ", pdfSettings.OutputFile);
            return stringBuilder.ToString();
        }

        private void GeneratePdfInternal(WkHtmlInput[] htmlFiles, string inputContent, string coverHtml, string outputPdfFilePath, Stream outputStream)
        {
            CheckWkHtmlProcess();
            string tempPath = GetTempPath();
            PdfSettings pdfSettings = new PdfSettings
            {
                InputFiles = htmlFiles,
                OutputFile = outputPdfFilePath
            };
            List<string> list = new List<string>();
            pdfSettings.CoverFilePath = ((!string.IsNullOrEmpty(coverHtml)) ? CreateTempFile(coverHtml, tempPath, list) : null);
            pdfSettings.HeaderFilePath = ((!string.IsNullOrEmpty(PageHeaderHtml)) ? CreateTempFile($"<!DOCTYPE html><html><head>\r\n<meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\" />\r\n<script>\r\nfunction subst() {{\r\n    var vars={{}};\r\n    var x=document.location.search.substring(1).split('&');\r\n\r\n    for(var i in x) {{var z=x[i].split('=',2);vars[z[0]] = unescape(z[1]);}}\r\n    var x=['frompage','topage','page','webpage','section','subsection','subsubsection'];\r\n    for(var i in x) {{\r\n      var y = document.getElementsByClassName(x[i]);\r\n      for(var j=0; j<y.length; ++j) y[j].textContent = vars[x[i]];\r\n    }}\r\n}}\r\n</script></head><body style=\"border:0; margin: 0;\" onload=\"subst()\">{PageHeaderHtml}</body></html>\r\n", tempPath, list) : null);
            pdfSettings.FooterFilePath = ((!string.IsNullOrEmpty(PageFooterHtml)) ? CreateTempFile($"<!DOCTYPE html><html><head>\r\n<meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\" />\r\n<script>\r\nfunction subst() {{\r\n    var vars={{}};\r\n    var x=document.location.search.substring(1).split('&');\r\n\r\n    for(var i in x) {{var z=x[i].split('=',2);vars[z[0]] = unescape(z[1]);}}\r\n    var x=['frompage','topage','page','webpage','section','subsection','subsubsection'];\r\n    for(var i in x) {{\r\n      var y = document.getElementsByClassName(x[i]);\r\n      for(var j=0; j<y.length; ++j) y[j].textContent = vars[x[i]];\r\n    }}\r\n}}\r\n</script></head><body style=\"border:0; margin: 0;\" onload=\"subst()\">{PageFooterHtml}</body></html>\r\n", tempPath, list) : null);
            if (pdfSettings.InputFiles != null)
            {
                WkHtmlInput[] inputFiles = pdfSettings.InputFiles;
                foreach (WkHtmlInput wkHtmlInput in inputFiles)
                {
                    wkHtmlInput.HeaderFilePath = ((!string.IsNullOrEmpty(wkHtmlInput.PageHeaderHtml)) ? CreateTempFile($"<!DOCTYPE html><html><head>\r\n<meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\" />\r\n<script>\r\nfunction subst() {{\r\n    var vars={{}};\r\n    var x=document.location.search.substring(1).split('&');\r\n\r\n    for(var i in x) {{var z=x[i].split('=',2);vars[z[0]] = unescape(z[1]);}}\r\n    var x=['frompage','topage','page','webpage','section','subsection','subsubsection'];\r\n    for(var i in x) {{\r\n      var y = document.getElementsByClassName(x[i]);\r\n      for(var j=0; j<y.length; ++j) y[j].textContent = vars[x[i]];\r\n    }}\r\n}}\r\n</script></head><body style=\"border:0; margin: 0;\" onload=\"subst()\">{wkHtmlInput.PageHeaderHtml}</body></html>\r\n", tempPath, list) : null);
                    wkHtmlInput.FooterFilePath = ((!string.IsNullOrEmpty(wkHtmlInput.PageFooterHtml)) ? CreateTempFile($"<!DOCTYPE html><html><head>\r\n<meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\" />\r\n<script>\r\nfunction subst() {{\r\n    var vars={{}};\r\n    var x=document.location.search.substring(1).split('&');\r\n\r\n    for(var i in x) {{var z=x[i].split('=',2);vars[z[0]] = unescape(z[1]);}}\r\n    var x=['frompage','topage','page','webpage','section','subsection','subsubsection'];\r\n    for(var i in x) {{\r\n      var y = document.getElementsByClassName(x[i]);\r\n      for(var j=0; j<y.length; ++j) y[j].textContent = vars[x[i]];\r\n    }}\r\n}}\r\n</script></head><body style=\"border:0; margin: 0;\" onload=\"subst()\">{wkHtmlInput.PageFooterHtml}</body></html>\r\n", tempPath, list) : null);
                }
            }
            try
            {
                if (inputContent != null)
                {
                    pdfSettings.InputFiles = new WkHtmlInput[1]
                    {
                        new WkHtmlInput(CreateTempFile(inputContent, tempPath, list))
                    };
                }
                if (outputStream != null)
                {
                    pdfSettings.OutputFile = CreateTempFile(null, tempPath, list);
                }
                if (batchMode)
                {
                    InvokeWkHtmlToPdfInBatch(pdfSettings);
                }
                else
                {
                    InvokeWkHtmlToPdf(pdfSettings, null, null);
                }
                if (outputStream != null)
                {
                    using (FileStream inputStream = new FileStream(pdfSettings.OutputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        CopyStream(inputStream, outputStream, 65536);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!batchMode)
                {
                    EnsureWkHtmlProcessStopped();
                }
                throw new Exception("Cannot generate PDF: " + ex.Message, ex);
            }
            finally
            {
                foreach (string item in list)
                {
                    DeleteFileIfExists(item);
                }
            }
        }

        private void InvokeWkHtmlToPdfInBatch(PdfSettings pdfSettings)
        {
            string lastErrorLine = string.Empty;
            DataReceivedEventHandler value = delegate (object o, DataReceivedEventArgs args)
            {
                if (args.Data != null)
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lastErrorLine = args.Data;
                    }
                    LogReceived?.Invoke(this, args);
                }
            };
            if (WkHtmlToPdfProcess == null || WkHtmlToPdfProcess.HasExited)
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo(GetToolExePath(), "--read-args-from-stdin");
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(PdfToolPath);
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.RedirectStandardOutput = false;
                processStartInfo.RedirectStandardError = true;
                WkHtmlToPdfProcess = Process.Start(processStartInfo);
                WkHtmlToPdfProcess.BeginErrorReadLine();
            }
            WkHtmlToPdfProcess.ErrorDataReceived += value;
            try
            {
                if (File.Exists(pdfSettings.OutputFile))
                {
                    File.Delete(pdfSettings.OutputFile);
                }
                string text = ComposeArgs(pdfSettings);
                text = text.Replace('\\', '/');
                WkHtmlToPdfProcess.StandardInput.WriteLine(text);
                bool flag = true;
                while (flag)
                {
                    Thread.Sleep(25);
                    if (WkHtmlToPdfProcess.HasExited)
                    {
                        flag = false;
                    }
                    if (File.Exists(pdfSettings.OutputFile))
                    {
                        flag = false;
                        WaitForFile(pdfSettings.OutputFile);
                    }
                }
                if (WkHtmlToPdfProcess.HasExited)
                {
                    CheckExitCode(WkHtmlToPdfProcess.ExitCode, lastErrorLine, File.Exists(pdfSettings.OutputFile));
                }
            }
            finally
            {
                if (WkHtmlToPdfProcess != null && !WkHtmlToPdfProcess.HasExited)
                {
                    WkHtmlToPdfProcess.ErrorDataReceived -= value;
                }
                else
                {
                    EnsureWkHtmlProcessStopped();
                }
            }
        }

        private void WaitForFile(string fullPath)
        {
            double num = (ExecutionTimeout.HasValue && ExecutionTimeout.Value != TimeSpan.Zero) ? ExecutionTimeout.Value.TotalMilliseconds : 60000.0;
            int num2 = 0;
            while (num > 0.0)
            {
                num2++;
                num -= 50.0;
                try
                {
                    using (FileStream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 100))
                    {
                        fileStream.ReadByte();
                    }
                }
                catch (Exception)
                {
                    Thread.Sleep((num2 < 10) ? 50 : 100);
                    continue;
                }
                break;
            }
            if (num == 0.0 && WkHtmlToPdfProcess != null && !WkHtmlToPdfProcess.HasExited)
            {
                WkHtmlToPdfProcess.StandardInput.Dispose();
                WkHtmlToPdfProcess.WaitForExit();
            }
        }

        private void InvokeWkHtmlToPdf(PdfSettings pdfSettings, string inputContent, Stream outputStream)
        {
            string lastErrorLine = string.Empty;
            DataReceivedEventHandler value = delegate (object o, DataReceivedEventArgs args)
            {
                if (args.Data != null)
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lastErrorLine = args.Data;
                    }
                    LogReceived?.Invoke(this, args);
                }
            };
            byte[] array = (inputContent != null) ? Encoding.UTF8.GetBytes(inputContent) : null;
            try
            {
                string arguments = ComposeArgs(pdfSettings);
                ProcessStartInfo processStartInfo = new ProcessStartInfo(GetToolExePath(), arguments);
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                processStartInfo.WorkingDirectory = Path.GetDirectoryName(PdfToolPath);
                processStartInfo.RedirectStandardInput = (array != null);
                processStartInfo.RedirectStandardOutput = (outputStream != null);
                processStartInfo.RedirectStandardError = true;
                WkHtmlToPdfProcess = Process.Start(processStartInfo);
                WkHtmlToPdfProcess.ErrorDataReceived += value;
                WkHtmlToPdfProcess.BeginErrorReadLine();
                if (array != null)
                {
                    WkHtmlToPdfProcess.StandardInput.BaseStream.Write(array, 0, array.Length);
                    WkHtmlToPdfProcess.StandardInput.BaseStream.Flush();
                    WkHtmlToPdfProcess.StandardInput.Dispose();
                }
                long num = 0L;
                if (outputStream != null)
                {
                    num = ReadStdOutToStream(WkHtmlToPdfProcess, outputStream);
                }
                WaitWkHtmlProcessForExit();
                if (outputStream == null && File.Exists(pdfSettings.OutputFile))
                {
                    num = new FileInfo(pdfSettings.OutputFile).Length;
                }
                CheckExitCode(WkHtmlToPdfProcess.ExitCode, lastErrorLine, num > 0);
            }
            finally
            {
                EnsureWkHtmlProcessStopped();
            }
        }

        /// <summary>
        /// Intiates PDF processing in the batch mode (generate several PDF documents using one wkhtmltopdf process) 
        /// </summary>
        public void BeginBatch()
        {
            if (batchMode)
            {
                throw new InvalidOperationException("HtmlToPdfConverter is already in the batch mode.");
            }
            batchMode = true;
        }

        /// <summary>
        /// Ends PDF processing in the batch mode.
        /// </summary>
        public void EndBatch()
        {
            if (!batchMode)
            {
                throw new InvalidOperationException("HtmlToPdfConverter is not in the batch mode.");
            }
            batchMode = false;
            if (WkHtmlToPdfProcess != null)
            {
                if (!WkHtmlToPdfProcess.HasExited)
                {
                    WkHtmlToPdfProcess.StandardInput.Dispose();
                    WkHtmlToPdfProcess.WaitForExit();
                }
                WkHtmlToPdfProcess = null;
            }
        }

        private void WaitWkHtmlProcessForExit()
        {
            if (ExecutionTimeout.HasValue)
            {
                if (WkHtmlToPdfProcess.WaitForExit((int)ExecutionTimeout.Value.TotalMilliseconds))
                {
                    return;
                }
                EnsureWkHtmlProcessStopped();
                throw new WkHtmlToPdfException(-2, $"WkHtmlToPdf process exceeded execution timeout ({ExecutionTimeout}) and was aborted");
            }
            WkHtmlToPdfProcess.WaitForExit();
        }

        private void EnsureWkHtmlProcessStopped()
        {
            if (WkHtmlToPdfProcess != null)
            {
                if (!WkHtmlToPdfProcess.HasExited)
                {
                    try
                    {
                        WkHtmlToPdfProcess.Kill();
                        WkHtmlToPdfProcess = null;
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    WkHtmlToPdfProcess = null;
                }
            }
        }

        private int ReadStdOutToStream(Process proc, Stream outputStream)
        {
            byte[] array = new byte[32768];
            int num = 0;
            int num2;
            while ((num2 = proc.StandardOutput.BaseStream.Read(array, 0, array.Length)) > 0)
            {
                outputStream.Write(array, 0, num2);
                num += num2;
            }
            return num;
        }

        private void CheckExitCode(int exitCode, string lastErrLine, bool outputNotEmpty)
        {
            int num;
            switch (exitCode)
            {
                case 0:
                    return;
                case 1:
                    num = ((Array.IndexOf(ignoreWkHtmlToPdfErrLines, lastErrLine.Trim()) >= 0) ? 1 : 0);
                    break;
                default:
                    num = 0;
                    break;
            }
            if ((num & (outputNotEmpty ? 1 : 0)) == 0)
            {
                throw new WkHtmlToPdfException(exitCode, lastErrLine);
            }
        }

        private void DeleteFileIfExists(string filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                try { File.Delete(filePath); }
                catch { }
            }
        }

        private void CopyStream(Stream inputStream, Stream outputStream, int bufSize)
        {
            byte[] array = new byte[bufSize];
            int count;
            while ((count = inputStream.Read(array, 0, array.Length)) > 0)
            {
                outputStream.Write(array, 0, count);
            }
        }
    }
}
