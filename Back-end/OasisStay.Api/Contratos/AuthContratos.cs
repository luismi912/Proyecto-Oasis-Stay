using System.ComponentModel.DataAnnotations;

namespace OasisStay.Api.Contratos;

public class LoginSolicitud
{
    [Required] public string Usuario { get; set; } = "";
    [Required] public string Contrasena { get; set; } = "";
}

public class LoginRespuesta
{
    public string Token { get; set; } = "";
    public string Rol { get; set; } = "";
    public string Nombre { get; set; } = "";
    public DateTime ExpiraEn { get; set; }
}

public class YoRespuesta
{
    public string Nombre { get; set; } = "";
    public string Rol { get; set; } = "";
}
