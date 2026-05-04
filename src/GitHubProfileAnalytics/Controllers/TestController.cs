using Microsoft.AspNetCore.Mvc;

namespace GitHubProfileAnalytics.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController: ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        var items = new[]
        {
            new { Id = 1, Name = "Alice" },
            new { Id = 2, Name = "Bob" },
        };

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        if (id <= 0)
            return BadRequest("Id must be > 0");
        
        var item = new {Id = id, Name = $"User #{id}"};
        
        return Ok(item);
    }
}