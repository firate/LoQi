using Microsoft.AspNetCore.Mvc;
using LoQi.API.Controllers.Base;
using LoQi.API.Models;
using LoQi.Application.Common;
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

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] LogSearchDto dto)
    {
        var result = await _logService.SearchLogsAsync(dto);

        if (result?.Items?.Count <= 0)
        {
            var notFoundResponse = ApiResponse<List<LogDto>>.NotFound();

            return NotFound(notFoundResponse);
        }

        var logs = new ApiResponse<List<LogDto>>(
            success:true,
            data:result?.Items ?? [],
            error:null,
            errors: null,
            pagination: result?.PaginationInfo ?? PaginationInfo.Empty());


        return Ok(logs);
    }
}