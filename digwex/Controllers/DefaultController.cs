using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Digwex.Data;
using Digwex.Models;

namespace Digwex.Controllers
{
  public class DefaultController : ControllerBase
  {
    protected readonly ApplicationDbContext _context;

    protected DefaultController(ApplicationDbContext context)
    {
      _context = context;
    }

    protected async Task<ApplicationUser> UserAsync()
    {
      var user = await _context.Users.SingleOrDefaultAsync(s => s.Id == User.Identity.Name);
      return user;
    }
  }
}
