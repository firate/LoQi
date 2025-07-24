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

// 🎯 Log Metadata Types - API'den gelen statik bilgiler
export interface LogMetadata {
    logLevels: LogLevelOption[];
    orderByOptions: OrderByOption[];
    pageSizeOptions: PageSizeOption[];
    sortOrderOptions: SortOrderOption[];
}

export interface LogLevelOption {
    value: number;
    label: string;
    color: string;  // CSS class: "text-red-600"
}

export interface OrderByOption {
    value: string;   // "timestamp", "level", "source"
    label: string;   // "Timestamp", "Level", "Source"
}

export interface PageSizeOption {
    value: number;   // 10, 25, 50, 100
    label: string;   // "10 per page", "25 per page"
}

export interface SortOrderOption {
    value: string;      // "desc", "asc"
    label: string;      // "Newest First", "Oldest First"
    isDescending: boolean;  // true, false
}