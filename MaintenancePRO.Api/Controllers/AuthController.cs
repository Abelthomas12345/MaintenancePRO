using System.Security.Claims; using MaintenancePRO.Api.Contracts; using MaintenancePRO.Api.Data; using MaintenancePRO.Api.Models; using MaintenancePRO.Api.Security; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Identity; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.RateLimiting;
namespace MaintenancePRO.Api.Controllers;
[ApiController,Route("api/auth")]
public sealed class AuthController(UserManager<ApplicationUser> users,SignInManager<ApplicationUser> signIn,TokenService tokens,AppDbContext db):ControllerBase {
 [HttpPost("register"),EnableRateLimiting("auth")] public async Task<IActionResult> Register(RegisterRequest r){
 var email=r.Email.Trim().ToLowerInvariant();var u=new ApplicationUser{UserName=email,Email=email,DisplayName=r.DisplayName.Trim(),Status=AccountStatus.Pending};
 var result=await users.CreateAsync(u,r.Password);if(!result.Succeeded)return BadRequest(new{message="Création impossible.",errors=result.Errors.Select(e=>e.Code)});
 await users.AddToRoleAsync(u,AppRoles.User);db.AuditEvents.Add(new AuditEvent{ActorUserId=u.Id,Action="AccountRegistered",EntityType="User",EntityId=u.Id,IpAddress=HttpContext.Connection.RemoteIpAddress?.ToString()});await db.SaveChangesAsync();
 return StatusCode(201,new{message="Compte créé. L'administrateur doit l'activer avant la connexion."});}
 [HttpPost("login"),EnableRateLimiting("auth")] public async Task<ActionResult<AuthResponse>> Login(LoginRequest r){
 var u=await users.FindByEmailAsync(r.Email.Trim());if(u is null||u.Status!=AccountStatus.Active)return Unauthorized(new{message="Identifiants invalides ou compte non actif."});
 var result=await signIn.CheckPasswordSignInAsync(u,r.Password,true);if(!result.Succeeded)return Unauthorized(new{message="Identifiants invalides ou compte temporairement verrouillé."});
 var (token,expires)=await tokens.CreateAsync(u);var roles=(await users.GetRolesAsync(u)).ToArray();
 db.AuditEvents.Add(new AuditEvent{ActorUserId=u.Id,Action="LoginSucceeded",EntityType="User",EntityId=u.Id,IpAddress=HttpContext.Connection.RemoteIpAddress?.ToString()});await db.SaveChangesAsync();
 return Ok(new AuthResponse(token,expires,u.Email!,u.DisplayName,roles));}
 [Authorize,HttpGet("me")] public async Task<IActionResult> Me(){var id=User.FindFirstValue(ClaimTypes.NameIdentifier);var u=id==null?null:await users.FindByIdAsync(id);return u==null?NotFound():Ok(new{id=u.Id,email=u.Email,displayName=u.DisplayName,status=u.Status,roles=await users.GetRolesAsync(u)});}
}
