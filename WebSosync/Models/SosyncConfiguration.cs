namespace WebSosync.Models
{
    public class SosyncConfiguration
    {
        public string Conf { get; set; }
        public string C => Conf;

        public int Port { get; set; }
        public int P => Port;

        public string Log_File { get; set; }
    }
}