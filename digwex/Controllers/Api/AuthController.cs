using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.V3.Pages.Account.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Digwex.Models;

namespace Digwex.Controllers.Api
{
  [ApiController]
  [Route("api/[controller]")]
  public class AuthController : ControllerBase
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;

    public AuthController(SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager, ILogger<LoginModel> logger)
    {
      _userManager = userManager;
      _signInManager = signInManager;
      _logger = logger;
    }

    /// <summary>
    /// Регистрация нового пользователя
    /// </summary>
    /// <remarks>
    /// После успешной регистрации, данные можно применить в методе /login для получения токена авторизации.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<object> Register(AuthRegisterModel model)
    {
      if (ModelState.IsValid) {

        if (model.password != model.confirmPassword) {
          Response.StatusCode = 400;
          return new { ok = false, msg = "Invalid confirm password" };
        }

        var user = new ApplicationUser { UserName = model.email, Email = model.email };
        var result = await _userManager.CreateAsync(user, model.password);
        if (result.Succeeded) {
          _logger.LogInformation("User created a new account with password.");

          //var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
          //code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
          //var callbackUrl = Url.Page(
          //    "/Account/ConfirmEmail",
          //    pageHandler: null,
          //    values: new { area = "Identity", userId = user.Id, code = code },
          //    protocol: Request.Scheme);

          //await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
          //    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

          //if (_userManager.Options.SignIn.RequireConfirmedAccount) {
          //  return RedirectToPage("RegisterConfirmation", new { email = Input.Email });
          //}
          //else {
          //await _signInManager.SignInAsync(user, isPersistent: false);
          Response.Cookies.Delete(".AspNetCore.Identity.Application");
          Response.Headers.Remove("Set-Cookie");
          //}

          return new { ok = true };
        }
        return result.Errors;
      }
      return new { ok = false, msg = "Invalid model" };
    }

    /// <summary>
    /// Вход пользователя
    /// </summary>
    /// <remarks>
    /// В случае успеха будет получен accessToken, который нужно использовать для авторизации. 
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(AuthLoginModel model)
    {
      if (ModelState.IsValid) {
        // This doesn't count login failures towards account lockout
        // To enable password failures to trigger account lockout, set lockoutOnFailure: true

        ApplicationUser user = await _userManager.FindByNameAsync(model.email);

        if (user == null) return NotFound();

        bool ok = await _userManager.CheckPasswordAsync(user, model.password);

        if (ok) {
          _logger.LogInformation("User logged in.");

          var tokenHandler = new JwtSecurityTokenHandler();
          var key = Encoding.ASCII.GetBytes(Startup.JWT_SECRET_KEY);
          var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(new Claim[]
            {
              new Claim(ClaimTypes.Name, user.Id)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
          };
          var token = tokenHandler.CreateToken(tokenDescriptor);

          return Ok(new { accessToken = tokenHandler.WriteToken(token), email = user.Email, companyName = Startup.SetupModel.Company?.Name });
        }
        //if (result.RequiresTwoFactor) {
        //  return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
        //}
        //if (result.IsLockedOut) {
        //  _logger.LogWarning("User account locked out.");
        //  return RedirectToPage("./Lockout");
        //}
        else {
          return BadRequest(new { msg = "Invalid login attempt." });
        }
      }

      // If we got this far, something failed, redisplay form
      return BadRequest(new { msg = "Invalid model" });
    }

    /// <summary>
    /// Восстановление пароля
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot(ForgotModel model)
    {
      return Ok();
    }

    /// <summary>
    /// Подтверждение восстановленяи пароля
    /// </summary>
    [AllowAnonymous]
    [HttpPost("confirmcode")]
    public async Task<IActionResult> Confirm(ConfirmCode model)
    {
      return Ok();
    }

    /// <summary>
    /// На почте будет ссылка, по которой кликнв будет редирект на страницу смены пароля. Так же будет и другая ссылка где можно ввести код
    /// </summary>
    [AllowAnonymous]
    [HttpGet("/auth/confirmcode")]
    public async Task<IActionResult> Confirm([FromQuery] string code)
    {
      return Redirect("/");
    }
  }

  // Models - hello swaggers:)

  public class ForgotModel
  {
    [Required]
    public string email { get; set; }
  }

  public class ConfirmCode
  {
    [Required]
    public string code { get; set; }

    [Required]
    public string password { get; set; }

    [Required]
    public string confirmPassword { get; set; }
  }

  public class AuthLoginModel
  {
    [Required]
    public string email { get; set; }

    [Required]
    public string password { get; set; }
  }

  public class AuthRegisterModel
  {
    [Required]
    public string email { get; set; }

    [Required]
    public string password { get; set; }

    [Required]
    public string confirmPassword { get; set; }
  }

  public class LoginResponse
  {
    string email { get; set; }
  }
}
