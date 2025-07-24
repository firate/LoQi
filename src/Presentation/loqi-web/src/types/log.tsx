export interface LogDto {
    uniqueId: string;
    correlationId?: string;
    message: string;
    source: string;
    levelId: number;
    date: string;
}

export interface LogSearchDto {
    uniqueId?: string;
    searchText?: string;
    startDate?: string;
    endDate?: string;
    levelId?: number;
    source?: string;
    correlationId?: string;
    page: number;
    pageSize: number;
    orderBy: string;
    descending: boolean;
}



export interface LogEntry {
    id: number;
    message: string;
    level: number;
    source: string;
    timestamp: string;
}


export interface ApiResponse<T> {
    success: boolean;
    data: T;
    pagination?: PaginationInfo,
    error?: string;
    errors?: Array<{
        field: string;
        message: string;
        attemptedValue?: unknown;
    }>;
    timestamp: string;
}

export interface PaginationInfo {
    endIndex: number,
    hasNextPage: boolean,
    hasPreviousPage: boolean,
    isFirstPage: boolean,
    isLastPage: boolean,
    page: number,
    pageSize: number,
    startIndex: number,
    totalCount: number,
    totalPages: number
}

