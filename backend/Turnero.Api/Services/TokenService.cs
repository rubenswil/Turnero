using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Turnero.Api.Domain;

namespace Turnero.Api.Services;

public class TokenService(IConfiguration config)
{
    public string Generar(Usuario usuario)
    {
        var clave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["Jwt:Clave"]!));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim("role", usuario.Rol.ToString()),
            new Claim("nombre", usuario.NombreVisible),
            new Claim("empleadoId", usuario.EmpleadoId?.ToString() ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Emisor"],
            audience: config["Jwt:Audiencia"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(clave, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
