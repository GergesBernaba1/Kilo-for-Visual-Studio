namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloFileDiff
    {
        public string FilePath { get; set; } = string.Empty;

        public string Before { get; set; } = string.Empty;

        public string After { get; set; } = string.Empty;

        public int Additions { get; set; }

        public int Deletions { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
