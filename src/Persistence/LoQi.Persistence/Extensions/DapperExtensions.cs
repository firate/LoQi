using System.Data;
using Dapper;
using LoQi.Application.Common;

namespace LoQi.Persistence.Extensions;


public static class DapperExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IDbConnection connection,
        string selectSql,
        string countSql,
        object parameters,
        int page,
        int pageSize)
    {
        // Dapper ile count query
        var totalCount = await connection.QuerySingleAsync<int>(countSql, parameters);
        
        // Pagination için SQL'e LIMIT OFFSET ekle
        var pagedSql = $"{selectSql} LIMIT @PageSize OFFSET @Offset";
        var pagedParams = new DynamicParameters(parameters);
        pagedParams.Add("@PageSize", pageSize);
        pagedParams.Add("@Offset", (page - 1) * pageSize);
        
        var items = (await connection.QueryAsync<T>(pagedSql, pagedParams)).ToList();

        return PagedResult<T>.Create(items, totalCount, page, pageSize);
    }

    // Generic repository extension
    public static async Task<PagedResult<T>> QueryPagedAsync<T>(
        this IDbConnection connection,
        string tableName,
        string whereClause = "",
        string orderBy = "Id",
        object parameters = null,
        int page = 1,
        int pageSize = 10)
    {
        var whereFilter = string.IsNullOrWhiteSpace(whereClause) ? "" : $"WHERE {whereClause}";
        
        var countSql = $"SELECT COUNT(*) FROM {tableName} {whereFilter}";
        var selectSql = $"SELECT * FROM {tableName} {whereFilter} ORDER BY {orderBy}";

        return await connection.ToPagedResultAsync<T>(selectSql, countSql, parameters, page, pageSize);
    }
}