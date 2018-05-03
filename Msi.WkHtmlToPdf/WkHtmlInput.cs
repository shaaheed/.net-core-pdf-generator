namespace Msi.WkHtmlToPdf
{
    public class WkHtmlInput
    {
        /// <summary>
        /// HTML file name or URL.
        /// </summary>
        public string Input { get; set; }

        /// <summary>
        /// Custom WkHtmlToPdf page options for this input.
        /// </summary>
        public string CustomWkHtmlPageArgs { get; set; }

        /// <summary>
        /// Get or set custom page header HTML for this input.
        /// </summary>
        public string PageHeaderHtml { get; set; }

        /// <summary>
        /// Get or set custom page footer HTML for this input.
        /// </summary>
        public string PageFooterHtml { get;  set; }

        internal string HeaderFilePath { get; set; }

        internal string FooterFilePath { get; set; }

        public WkHtmlInput(string inputFileOrUrl)
        {
            Input = inputFileOrUrl;
        }
    }
}
