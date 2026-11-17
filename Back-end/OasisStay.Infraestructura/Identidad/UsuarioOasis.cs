using Microsoft.AspNetCore.Identity;

namespace OasisStay.Infraestructura.Identidad;

/// <summary>Usuario de Identity con Nombre; Administrador/Recepcionista/AmaDeLlaves son roles [D-02].</summary>
public class UsuarioOasis : IdentityUser
{
    public string Nombre { get; set; } = "";
}

/// <summary>Nombres de los 3 roles [E1 RNF-06][D-02].</summary>
public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Recepcionista = "Recepcionista";
    public const string AmaDeLlaves = "AmaDeLlaves";
    public static readonly string[] Todos = { Administrador, Recepcionista, AmaDeLlaves };
}
