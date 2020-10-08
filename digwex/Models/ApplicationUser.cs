using Microsoft.AspNetCore.Identity;
using Digwex.Data;

namespace Digwex.Models
{
  public class ApplicationUser : IdentityUser
  {
    public SettingsEntity Settings { get; set; }

    public bool Owner { get; set; }
  }
}
