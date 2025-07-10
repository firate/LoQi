using Microsoft.AspNetCore.Mvc;

using LoQi.API.Controllers.Base;
using LoQi.Application.DTOs;
using LoQi.Application.Service;

namespace LoQi.API.Controllers;

public class LogController : BaseApiController
{
    private ILogService _logService;

    public LogController(ILogService logService)
    {
        _logService = logService;
    }

    // TODO: should be deleted
    [HttpPost]
    public async Task<IActionResult> AddLogAsync([FromBody] AddLogDto dto)
    {
        var isRecorded = await _logService.AddLogAsync(dto);

        if (isRecorded)
        {
            return Ok();
        }

        return BadRequest();
    }
}