using Microsoft.AspNetCore.Mvc;

using LoQi.API.Controllers.Base;
using LoQi.Application.DTOs;
using LoQi.Application.Services;

namespace LoQi.API.Controllers;

public class LogController : BaseApiController
{
    private ILogService _logService;

    public LogController(ILogService logService)
    {
        _logService = logService;
    }

    [HttpPost]
    public async Task<IActionResult> Search([FromBody] LogSearchDto dto)
    {
       
        return BadRequest();
    }
}