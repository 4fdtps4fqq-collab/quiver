using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KiteFlow.Gateway.Controllers;

[ApiController]
[Authorize(Policy = "SystemAdminOnly")]
[Route("api/v1/onboarding")]
public sealed class OnboardingController : ControllerBase
{
    [HttpPost("register-owner")]
    public IActionResult RegisterOwner()
    {
        return StatusCode(StatusCodes.Status410Gone, new
        {
            message = "Use /api/v1/system/schools para cadastrar novas escolas e enviar a senha temporária do proprietário."
        });
    }
}
