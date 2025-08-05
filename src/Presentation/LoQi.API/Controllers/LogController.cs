using Microsoft.AspNetCore.Mvc;
using LoQi.API.Controllers.Base;
using LoQi.API.Dtos;
using LoQi.API.Models;
using LoQi.Application.Common;
using LoQi.Application.DTOs;
using LoQi.Application.Services.Log;

namespace LoQi.API.Controllers;

public class LogController : BaseApiController
{
    private ILogService _logService;

    public LogController(ILogService logService)
    {
        _logService = logService;
    }

    [HttpGet("metadata")]
    public async Task<IActionResult> GetLogMetadata()
    {
        var metadata = new LogMetadataDto
        {
            LogLevels =
            [
                new LogLevelDto(0, "Verbose", "text-gray-600"),
                new LogLevelDto(1, "Debug", "text-blue-600"),
                new LogLevelDto(2, "Information", "text-green-600"),
                new LogLevelDto(3, "Warning", "text-yellow-600"),
                new LogLevelDto(4, "Error", "text-red-600"),
                new LogLevelDto(5, "Fatal", "text-red-800")
            ],
            OrderByOptions =
            [
                new OrderByOptionDto("timestamp", "Timestamp"),
                new OrderByOptionDto("level", "Level"),
                new OrderByOptionDto("source", "Source")
            ],
            PageSizeOptions =
            [
                new PageSizeOptionDto(10, "10 per page"),
                new PageSizeOptionDto(25, "25 per page"),
                new PageSizeOptionDto(50, "50 per page"),
                new PageSizeOptionDto(100, "100 per page")
            ],
            SortOrderOptions =
            [
                new SortOrderOptionDto("desc", "Newest First", true),
                new SortOrderOptionDto("asc", "Oldest First", false)
            ]
        };

        var response = await Task.Run(() => ApiResponse<LogMetadataDto>.Ok(metadata));

        return Ok(response);
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
            success: true,
            data: result?.Items ?? [],
            error: null,
            errors: null,
            pagination: result?.PaginationInfo ?? PaginationInfo.Empty());


        return Ok(logs);
    }

    [HttpGet("{uniqueId}")]
    public async Task<IActionResult> GetLogDetail([FromRoute] string uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            var badRequestResponse = ApiResponse<LogDto>.BadRequest("UniqueId is required");
            return BadRequest(badRequestResponse);
        }

        // Use search with uniqueId to get single log with full message
        var searchDto = new LogSearchDto
        {
            UniqueId = uniqueId,
            Page = 1,
            PageSize = 1,
            OrderBy = "timestamp",
            Descending = true
        };

        var result = await _logService.SearchLogsAsync(searchDto);

        if (result?.Items?.Count <= 0)
        {
            var notFoundResponse = ApiResponse<LogDto>.NotFound($"Log with UniqueId '{uniqueId}' not found");
            return NotFound(notFoundResponse);
        }

        var log = result?.Items?.First();
        var response = new ApiResponse<LogDto>(
            success: true,
            data: log,
            error: null,
            errors: null,
            pagination: result?.PaginationInfo ?? PaginationInfo.Empty());

        return Ok(response);
    }
}