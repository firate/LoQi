import {ApiResponse, LogDto} from '../types/log';
import {useRouter} from 'next/navigation';

interface LogSearchResultsProps {
    results: ApiResponse<LogDto[]> | null;
    loading: boolean;
    error: string | null;
    onPageChange: (page: number) => void;
}

export default function LogSearchResults({results, loading, error, onPageChange}: LogSearchResultsProps) {
    const router = useRouter();

    const getLevelColor = (level: number) => {
        switch (level) {
            case 0:
                return 'text-gray-600 bg-gray-100';
            case 1:
                return 'text-blue-600 bg-blue-100';
            case 2:
                return 'text-green-600 bg-green-100';
            case 3:
                return 'text-yellow-600 bg-yellow-100';
            case 4:
                return 'text-red-600 bg-red-100';
            case 5:
                return 'text-red-800 bg-red-100';
            default:
                return 'text-gray-600 bg-gray-100';
        }
    };

    const getLevelText = (level: number) => {
        const levels = ['VERBOSE', 'DEBUG', 'INFO', 'WARNING', 'ERROR', 'FATAL'];
        return levels[level] || 'UNKNOWN';
    };

    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleString('en-US', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        });
    };

    const handleViewDetails = (uniqueId: string) => {
        router.push(`/?logId=${uniqueId}`);
    };

    if (loading) {
        return (
            <div className="bg-white rounded-lg shadow-lg p-6">
                <div className="flex items-center justify-center py-12">
                    <svg className="animate-spin h-8 w-8 text-blue-600" xmlns="http://www.w3.org/2000/svg" fill="none"
                         viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor"
                                strokeWidth="4"></circle>
                        <path className="opacity-75" fill="currentColor"
                              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    <span className="ml-2 text-gray-600">Searching logs...</span>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="bg-white rounded-lg shadow-lg p-6">
                <div className="text-center py-12">
                    <div className="text-red-600 mb-2">
                        <svg className="mx-auto h-12 w-12" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                                  d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.732-.833-2.464 0L4.348 21c-.77.833.192 2.5 1.732 2.5z"/>
                        </svg>
                    </div>
                    <h3 className="text-lg font-medium text-gray-900 mb-2">Search Error</h3>
                    <p className="text-gray-600">{error}</p>
                </div>
            </div>
        );
    }

    if (!results) {
        return (
            <div className="bg-white rounded-lg shadow-lg p-6">
                <div className="text-center py-12 text-gray-500">
                    <svg className="mx-auto h-12 w-12 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                              d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                    </svg>
                    <p>Enter search criteria above to find logs</p>
                </div>
            </div>
        );
    }

    //const { logs, totalCount, page, totalPages, hasNextPage, hasPreviousPage } = results;

    const logs = results.data;
    const pagination = results.pagination;

    if (logs.length === 0) {
        return (
            <div className="bg-white rounded-lg shadow-lg p-6">
                <div className="text-center py-12 text-gray-500">
                    <svg className="mx-auto h-12 w-12 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                              d="M9 12h6m-3-6V4a1 1 0 00-1-1H7a1 1 0 00-1 1v4a1 1 0 001 1h1m0 0h4a1 1 0 001-1V7a1 1 0 00-1-1h-2M7 16h2m6 0h2"/>
                    </svg>
                    <h3 className="text-lg font-medium text-gray-900 mb-2">No Logs Found</h3>
                    <p>No logs match your search criteria. Try adjusting your filters.</p>
                </div>
            </div>
        );
    }

    return (
        <div className="bg-white rounded-lg shadow-lg p-6">
            {/* Results Header */}
            <div className="flex justify-between items-center mb-6">
                <div>
                    <h3 className="text-lg font-semibold text-gray-800">Search Results</h3>
                    <p className="text-sm text-gray-600">
                        Showing {logs.length} of {pagination?.totalCount} logs
                        (Page {pagination?.page} of {pagination?.totalPages})
                    </p>
                </div>
            </div>

            {/* Results List */}
            <div className="space-y-3 mb-6">
                {logs.map((log, index) => (
                    <div key={`${log.uniqueId}-${index}`}
                         className="border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow">
                        <div className="flex items-start justify-between">
                            <div className="flex-1">
                                <div className="flex items-center gap-3 mb-2">
                                    <span
                                        className={`text-xs font-mono px-2 py-1 rounded ${getLevelColor(log.levelId)}`}>
                                        {getLevelText(log.levelId)}
                                    </span>
                                    <span className="text-sm font-medium text-gray-700">{log.source}</span>
                                    <span className="text-xs text-gray-400">{formatDate(log.date)}</span>
                                    {log.correlationId && (
                                        <span className="text-xs text-blue-600 bg-blue-50 px-2 py-1 rounded">
                                            ID: {log.correlationId.slice(-8)}
                                        </span>
                                    )}
                                </div>
                                <p className="text-gray-800 break-words leading-relaxed">{log.message}</p>
                                {log.uniqueId && (
                                    <p className="text-xs text-gray-400 mt-2 font-mono">
                                        Unique ID: {log.uniqueId}
                                    </p>
                                )}
                            </div>
                            <div className="ml-4 flex-shrink-0">
                                <button
                                    onClick={() => handleViewDetails(log.uniqueId)}
                                    className="px-3 py-1 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors"
                                >
                                    View Details
                                </button>
                            </div>
                        </div>
                    </div>
                ))}
            </div>

            {/* Pagination */}
            {(pagination?.totalPages ?? 1) > 1 && (
                <div className="flex items-center justify-between border-t pt-6">
                    <div className="flex items-center gap-2">
                        <button
                            onClick={() => onPageChange((pagination?.page ?? 1) - 1)}
                            disabled={!pagination?.hasPreviousPage}
                            className="px-3 py-2 text-sm text-black border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                        >
                            Previous
                        </button>

                        <div className="flex items-center gap-1">
                            {Array.from({length: Math.min(5, pagination?.totalPages ?? 1)}, (_, i) => {
                                const pageNum = Math.max(1, (Math.min(pagination?.totalPages ?? 1) - 4, (pagination?.page ?? 1) - 2)) + i;
                                return (
                                    <button
                                        key={pageNum}
                                        onClick={() => onPageChange(pageNum)}
                                        className={`px-3 py-2 text-sm rounded-md transition-colors text-black ${
                                            pageNum === pagination?.page
                                                ? 'bg-blue-600 text-white'
                                                : 'border border-gray-300 hover:bg-gray-50'
                                        }`}
                                    >
                                        {pageNum}
                                    </button>
                                );
                            })}
                        </div>

                        <button
                            onClick={() => onPageChange((pagination?.page ?? 1) + 1)}
                            disabled={!pagination?.hasNextPage}
                            className="px-3 py-2 text-sm text-black border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                        >
                            Next
                        </button>
                    </div>

                    <div className="text-sm text-black">
                        Page {pagination?.page} of {pagination?.totalPages}
                    </div>
                </div>
            )}
        </div>
    );
}