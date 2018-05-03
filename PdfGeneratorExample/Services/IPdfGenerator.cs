namespace PdfGeneratorExample
{
    public interface IPdfGenerator
    {
        byte[] Generate(string htmlContent);
    }
}
