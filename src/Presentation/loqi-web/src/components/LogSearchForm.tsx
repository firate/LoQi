import {useState, useEffect} from 'react';
import {LogSearchDto, LogMetadata} from '../types/log';
import {logService} from '../services/LogService';

interface LogSearchFormProps {
    onSearch: (params: LogSearchDto) => void;
    loading: boolean;
}

export default function LogSearchForm({onSearch, loading}: LogSearchFormProps) {
    const [formData, setFormData] = useState<Partial<LogSearchDto>>({
        searchText: '',
        startDate: '',
        endDate: '',
        levelId: undefined,
        source: '',
        page: 1,
        pageSize: 50,
        orderBy: 'timestamp',
        descending: true
    });

    const [metadata, setMetadata] = useState<LogMetadata | null>(null);
    const [metadataLoading, setMetadataLoading] = useState(true);
    const [metadataError, setMetadataError] = useState<string | null>(null);

    // Fetch metadata on component mount
    useEffect(() => {
        const fetchMetadata = async () => {
            try {
                setMetadataLoading(true);
                const response = await logService.getLogMetadata();
                if (response && response.success) {
                    setMetadata(response.data);
                } else {
                    setMetadataError(response?.error || 'Failed to load metadata');
                }
            } catch (error) {
                setMetadataError(error instanceof Error ? error.message : 'Failed to load metadata');
            } finally {
                setMetadataLoading(false);
            }
        };

        fetchMetadata();
    }, []);

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();

        const searchParams: LogSearchDto = {
            searchText: formData.searchText || undefined,
            startDate: formData.startDate || undefined,
            endDate: formData.endDate || undefined,
            levelId: formData.levelId,
            source: formData.source || undefined,
            page: 1, // Reset to first page on new search
            pageSize: formData.pageSize || 50,
            orderBy: formData.orderBy || 'timestamp',
            descending: formData.descending ?? true
        };

        onSearch(searchParams);
    };

    const handleQuickSearch = (days: number, hours: number, minutes: number) => {
        const endDate = new Date();
        const startDate = new Date();

        // Gün, saat ve dakikayı çıkar
        startDate.setDate(startDate.getDate() - days);
        startDate.setHours(startDate.getHours() - hours);
        startDate.setMinutes(startDate.getMinutes() - minutes);

        setFormData(prev => ({
            ...prev,
            startDate: startDate.toISOString().slice(0, 16),
            endDate: endDate.toISOString().slice(0, 16)
        }));
    };

    const clearForm = () => {
        setFormData({
            searchText: '',
            startDate: '',
            endDate: '',
            levelId: undefined,
            source: '',
            page: 1,
            pageSize: 50,
            orderBy: 'timestamp',
            descending: true
        });
    };

    // Show loading state for metadata
    if (metadataLoading) {
        return (
            <div className="bg-white rounded-lg shadow-lg p-6 mb-6">
                <div className="flex items-center justify-center py-8">
                    <svg className="animate-spin h-6 w-6 text-blue-600" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    <span className="ml-2 text-gray-600">Loading search options...</span>
                </div>
            </div>
        );
    }

    // Show error state for metadata
    if (metadataError || !metadata) {
        return (
            <div className="bg-white rounded-lg shadow-lg p-6 mb-6">
                <div className="text-center py-8">
                    <div className="text-red-600 mb-2">
                        <svg className="mx-auto h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01"/>
                        </svg>
                    </div>
                    <h3 className="text-lg font-medium text-gray-900 mb-2">Error Loading Search Options</h3>
                    <p className="text-gray-600">{metadataError}</p>
                </div>
            </div>
        );
    }

    return (
        <div className="bg-white rounded-lg shadow-lg p-6 mb-6">
            <div className="flex justify-between items-center mb-6">
                <h2 className="text-xl font-semibold text-gray-800">Search Logs</h2>
                <div className="flex gap-2">
                    <button
                        type="button"
                        onClick={() => handleQuickSearch(0, 0, 15)}
                        className="px-3 py-1 text-sm bg-blue-100 text-blue-700 rounded hover:bg-blue-200 transition-colors"
                    >
                        Last 15min
                    </button>
                    <button
                        type="button"
                        onClick={() => handleQuickSearch(0, 1, 0)}
                        className="px-3 py-1 text-sm bg-blue-100 text-blue-700 rounded hover:bg-blue-200 transition-colors"
                    >
                        Last 1h
                    </button>
                    <button
                        type="button"
                        onClick={() => handleQuickSearch(1, 0, 0)}
                        className="px-3 py-1 text-sm bg-blue-100 text-blue-700 rounded hover:bg-blue-200 transition-colors"
                    >
                        Last 24h
                    </button>
                </div>
            </div>

            <form onSubmit={handleSubmit} className="space-y-4">
                {/* Search Text */}
                <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                        Search Text
                    </label>
                    <input
                        type="text"
                        value={formData.searchText || ''}
                        onChange={(e) => setFormData(prev => ({...prev, searchText: e.target.value}))}
                        placeholder="Search in log messages..."
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                    />
                </div>

                {/* Date Range */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Start Date
                        </label>
                        <input
                            type="datetime-local"
                            value={formData.startDate || ''}
                            onChange={(e) => setFormData(prev => ({...prev, startDate: e.target.value}))}
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                        />
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            End Date
                        </label>
                        <input
                            type="datetime-local"
                            value={formData.endDate || ''}
                            onChange={(e) => setFormData(prev => ({...prev, endDate: e.target.value}))}
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                        />
                    </div>
                </div>

                {/* Level and Source */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Log Level
                        </label>
                        <select
                            value={formData.levelId ?? ''}
                            onChange={(e) => setFormData(prev => ({
                                ...prev,
                                levelId: e.target.value ? parseInt(e.target.value) : undefined
                            }))}
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                        >
                            <option value="">All Levels</option>
                            {metadata.logLevels.map(level => (
                                <option key={level.value} value={level.value}>
                                    {level.label}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Source
                        </label>
                        <input
                            type="text"
                            value={formData.source || ''}
                            onChange={(e) => setFormData(prev => ({...prev, source: e.target.value}))}
                            placeholder="e.g., PaymentService, UserController..."
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                        />
                    </div>
                </div>

                {/* Advanced Options */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Page Size
                        </label>
                        <select
                            value={formData.pageSize || 50}
                            onChange={(e) => setFormData(prev => ({...prev, pageSize: parseInt(e.target.value)}))}
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                        >
                            {metadata.pageSizeOptions.map(option => (
                                <option key={option.value} value={option.value}>
                                    {option.label}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Order By
                        </label>
                        <select
                            value={formData.orderBy || 'timestamp'}
                            onChange={(e) => setFormData(prev => ({...prev, orderBy: e.target.value}))}
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                        >
                            {metadata.orderByOptions.map(option => (
                                <option key={option.value} value={option.value}>
                                    {option.label}
                                </option>
                            ))}
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">
                            Sort Order
                        </label>
                        <select
                            value={formData.descending ? 'desc' : 'asc'}
                            onChange={(e) => setFormData(prev => ({...prev, descending: e.target.value === 'desc'}))}
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-gray-900"
                        >
                            {metadata.sortOrderOptions.map(option => (
                                <option key={option.value} value={option.value}>
                                    {option.label}
                                </option>
                            ))}
                        </select>
                    </div>
                </div>

                {/* Action Buttons */}
                <div className="flex gap-3 pt-4">
                    <button
                        type="submit"
                        disabled={loading}
                        className="flex-1 md:flex-none px-6 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                    >
                        {
                            loading ?
                                (
                                    <>
                                        <svg className="animate-spin -ml-1 mr-3 h-5 w-5 text-white inline"
                                             xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                                            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor"
                                                    strokeWidth="4"></circle>
                                            <path className="opacity-75" fill="currentColor"
                                                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                                        </svg>
                                        Searching...
                                    </>
                                )
                                :
                                (
                                    'Search'
                                )
                        }
                    </button>
                    <button
                        type="button"
                        onClick={clearForm}
                        className="px-6 py-2 border border-gray-300 text-gray-700 rounded-md hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition-colors"
                    >
                        Clear
                    </button>
                </div>
            </form>
        </div>
    );
}