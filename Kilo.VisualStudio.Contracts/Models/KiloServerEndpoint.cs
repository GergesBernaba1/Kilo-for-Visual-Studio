namespace Kilo.VisualStudio.Contracts.Models
{
    public class KiloServerEndpoint
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:4096";

        public string Password { get; set; } = string.Empty;

        public string Username { get; set; } = "kilo";
    }
}
