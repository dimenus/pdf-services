using Microsoft.AspNetCore.Mvc;

namespace PdfServices.Service.Controllers;

[Microsoft.AspNetCore.Components.Route("/")]
public class HomeController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult RunHealthCheck()
    {
        return Ok(new {
            Status = "Ok"
        });
    }
}