using Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Web;

[Route("[controller]")]
[ApiController]
public class TestController(ICalculator calculator) : ControllerBase
{
    [HttpGet]
    public Task<int> Get(int a, int b) => calculator.Add(a, b);
}
