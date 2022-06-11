using Microsoft.AspNetCore.Mvc;
using PdfServices.Service.Utils;

namespace PdfServices.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly SqliteDbContext _dbContext;

    public HealthController(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public string HealthCheck()
    {
        _ = _dbContext.GetConnection();
        return "Status is Nominal!";
    }
}