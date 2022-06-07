namespace WebSosync.Models
{
    public class SyncModelDetailsDto
    {
        public string Model { get; set; }
        public string ConcurrencyWinner { get; set; }
        public int Priority { get; set; }
        public string SyncDirection { get; set; }
    }
}
