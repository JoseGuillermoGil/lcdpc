namespace LCDPC.Domain.Entities.Users;

public class ApiToken
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public bool Activo { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ApiToken()
    {
    }

    public static ApiToken Create(Guid id, string nombre, string tokenHash)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id is required", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("nombre is required", nameof(nombre));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("tokenHash is required", nameof(tokenHash));
        }

        return new ApiToken
        {
            Id = id,
            Nombre = nombre.Trim(),
            TokenHash = tokenHash,
            Activo = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Desactivar() => Activo = false;
}
