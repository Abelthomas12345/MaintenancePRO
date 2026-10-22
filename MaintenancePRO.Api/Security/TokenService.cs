using System.IdentityModel.Tokens.Jwt; using System.Security.Claims; using System.Text; using MaintenancePRO.Api.Models; using Microsoft.AspNetCore.Identity; using Microsoft.IdentityModel.Tokens;
namespace MaintenancePRO.Api.Security;
public sealed class TokenService(IConfiguration c,UserManager<ApplicationUser> users) {
 public async Task<(string Token,DateTime Expires)> CreateAsync(ApplicationUser u){
  var now=DateTime.UtcNow; var mins=c.GetValue<int?>("Jwt:AccessTokenMinutes")??15;
  var claims=new List<Claim>{new(JwtRegisteredClaimNames.Sub,u.Id),new(JwtRegisteredClaimNames.Email,u.Email??""),new(ClaimTypes.NameIdentifier,u.Id),new(ClaimTypes.Name,u.UserName??u.Email??""),new(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString("N"))};
  foreach(var role in await users.GetRolesAsync(u))claims.Add(new Claim(ClaimTypes.Role,role));
  var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(c["Jwt:Key"]!)); var expires=now.AddMinutes(mins);
  var token=new JwtSecurityToken(c["Jwt:Issuer"],c["Jwt:Audience"],claims,now,expires,new SigningCredentials(key,SecurityAlgorithms.HmacSha256));
  return(new JwtSecurityTokenHandler().WriteToken(token),expires);
 }
}
