using dadi_data.Models;

namespace WebSosync.Data.Models;

public class MdbTokenDto
    : dboAktionOnlineToken
{
    public int PersonID { get; set; }
}
