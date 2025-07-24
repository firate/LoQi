"use client";
import { useState } from 'react';
import LogSearchForm from './LogSearchForm';
import LogSearchResults from './LogSearchResults';
import { logService } from '../services/LogService';
import { LogSearchDto, ApiResponse, LogDto } from '../types/log';

export default function LogSearchPage() {
    const [results, setResults] = useState<ApiResponse<LogDto[]> | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [currentSearchParams, setCurrentSearchParams] = useState<LogSearchDto | null>(null);

    const handleSearch = async (searchParams: LogSearchDto) => {
        setLoading(true);
        setError(null);
        setCurrentSearchParams(searchParams);

        try {
            const response = await logService.searchLogs(searchParams);

            if (response && response.success) {
                setResults(response);
            } else {
                setError(response?.error || 'Search failed');
                setResults(null);
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Search failed');
            setResults(null);
        } finally {
            setLoading(false);
        }
    };

    const handlePageChange = async (newPage: number) => {
        if (!currentSearchParams) return;

        const updatedParams = {
            ...currentSearchParams,
            page: newPage
        };

        await handleSearch(updatedParams);
    };

    return (
        <div className="min-h-screen bg-gray-100 p-4">
            <div className="max-w-7xl mx-auto">
                <LogSearchForm
                    onSearch={handleSearch}
                    loading={loading}
                />

                <LogSearchResults
                    results={results}
                    loading={loading}
                    error={error}
                    onPageChange={handlePageChange}
                />
            </div>
        </div>
    );
}