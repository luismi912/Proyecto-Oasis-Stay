using System.Security.Cryptography;
using System.Text;

namespace OasisStay.Infraestructura.Seguridad;

/// <summary>Cifrado de datos personales [D-03][E1 RNF-04].</summary>
public interface ICifradorDatos
{
    string Cifrar(string textoPlano);
    string Descifrar(string textoCifrado);
}

/// <summary>
/// AES-256-CBC determinista (IV sintético = HMAC-SHA256 del texto, estilo SIV): el mismo texto produce
/// siempre el mismo cifrado, de modo que la búsqueda por documento (US-03 ESC2) funciona con
/// igualdad sobre la columna cifrada [D-03]. Formato: Base64(IV || cifrado).
/// </summary>
public sealed class CifradorAes256 : ICifradorDatos
{
    private readonly byte[] _claveCifrado;
    private readonly byte[] _claveMac;

    public CifradorAes256(byte[] claveMaestra)
    {
        if (claveMaestra is not { Length: 32 })
            throw new ArgumentException("La clave AES debe tener 32 bytes (256 bits).", nameof(claveMaestra));
        _claveCifrado = HMACSHA256.HashData(claveMaestra, Encoding.UTF8.GetBytes("oasis-enc"));
        _claveMac = HMACSHA256.HashData(claveMaestra, Encoding.UTF8.GetBytes("oasis-mac"));
    }

    public string Cifrar(string textoPlano)
    {
        if (string.IsNullOrEmpty(textoPlano)) return textoPlano;
        var datos = Encoding.UTF8.GetBytes(textoPlano);
        var iv = HMACSHA256.HashData(_claveMac, datos)[..16];
        using var aes = Aes.Create();
        aes.Key = _claveCifrado;
        var cifrado = aes.EncryptCbc(datos, iv);
        return Convert.ToBase64String(iv.Concat(cifrado).ToArray());
    }

    public string Descifrar(string textoCifrado)
    {
        if (string.IsNullOrEmpty(textoCifrado)) return textoCifrado;
        var todo = Convert.FromBase64String(textoCifrado);
        using var aes = Aes.Create();
        aes.Key = _claveCifrado;
        var datos = aes.DecryptCbc(todo.AsSpan(16), todo.AsSpan(0, 16));
        return Encoding.UTF8.GetString(datos);
    }
}
