namespace LCDPC.Domain.Entities.Users;

public class Sede
{
    public Guid Id { get; set; }
    public string NombreTienda { get; set; } = string.Empty;
    public string Rif { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string TelefonoContacto { get; set; } = string.Empty;
    public string? TelefonoContactoSecundario { get; set; }
    public string HorarioAtencion { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
