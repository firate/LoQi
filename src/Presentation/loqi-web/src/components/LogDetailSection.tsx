"use client";
import { useState, useEffect } from 'react';
import { logService } from '../services/LogService';
import { LogDto } from '../types/log';

interface LogDetailSectionProps {
    uniqueId: string;
    onBack: () => void;
}

export default function LogDetailSection({ uniqueId, onBack }: LogDetailSectionProps) {
    const [log, setLog] = useState<LogDto | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [isJsonFormatted, setIsJsonFormatted] = useState(false);

    useEffect(() => {
        const fetchLogDetail = async () => {
            if (!uniqueId) return;

            setLoading(true);
            setError(null);

            try {
                const response = await logService.getLogByUniqueId(uniqueId);
                if (response && response.success) {
                    setLog(response.data);
                    // Check if message is JSON
                    try {
                        JSON.parse(response.data.message);
                        setIsJsonFormatted(true);
                    } catch {
                        setIsJsonFormatted(false);
                    }
                } else {
                    setError(response?.error || 'Log not found');
                }
            } catch (err) {
                setError(err instanceof Error ? err.message : 'Failed to fetch log details');
            } finally {
                setLoading(false);
            }
        };

        fetchLogDetail();
    }, [uniqueId]);

    const getLevelColor = (level: number) => {
        switch (level) {
            case 0: return 'text-gray-600 bg-gray-100';
            case 1: return 'text-blue-600 bg-blue-100';
            case 2: return 'text-green-600 bg-green-100';
            case 3: return 'text-yellow-600 bg-yellow-100';
            case 4: return 'text-red-600 bg-red-100';
            case 5: return 'text-red-800 bg-red-100';
            default: return 'text-gray-600 bg-gray-100';
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
            second: '2-digit',
            timeZoneName: 'short'
        });
    };

    const formatJsonMessage = (message: string) => {
        try {
            const parsed = JSON.parse(message);
            return JSON.stringify(parsed, null, 2);
        } catch {
            return message;
        }
    };

    const copyToClipboard = async (text: string, type: string) => {
        try {
            await navigator.clipboard.writeText(text);
            // You could add a toast notification here
            console.log(`${type} copied to clipboard`);
        } catch (err) {
            console.error('Failed to copy:', err);
        }
    };

    const downloadAsText = () => {
        if (!log) return;

        const content = `Log Details
===========
Unique ID: ${log.uniqueId}
Correlation ID: ${log.correlationId || 'N/A'}
Level: ${getLevelText(log.levelId)}
Source: ${log.source}
Timestamp: ${formatDate(log.date)}

Message:
${log.message}
`;

        const blob = new Blob([content], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `log-${log.uniqueId}.txt`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-gray-100 p-4">
                <div className="max-w-4xl mx-auto">
                    <div className="bg-white rounded-lg shadow-lg p-6">
                        <div className="flex items-center justify-center py-12">
                            <svg className="animate-spin h-8 w-8 text-blue-600" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                            </svg>
                            <span className="ml-2 text-gray-600">Loading log details...</span>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="min-h-screen bg-gray-100 p-4">
                <div className="max-w-4xl mx-auto">
                    <div className="bg-white rounded-lg shadow-lg p-6">
                        <div className="text-center py-12">
                            <div className="text-red-600 mb-4">
                                <svg className="mx-auto h-16 w-16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.732-.833-2.464 0L4.348 21c-.77.833.192 2.5 1.732 2.5z"/>
                                </svg>
                            </div>
                            <h3 className="text-lg font-medium text-gray-900 mb-2">Error Loading Log</h3>
                            <p className="text-gray-600 mb-4">{error}</p>
                            <button
                                onClick={onBack}
                                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors"
                            >
                                Go Back
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    if (!log) {
        return (
            <div className="min-h-screen bg-gray-100 p-4">
                <div className="max-w-4xl mx-auto">
                    <div className="bg-white rounded-lg shadow-lg p-6">
                        <div className="text-center py-12">
                            <h3 className="text-lg font-medium text-gray-900 mb-2">Log Not Found</h3>
                            <p className="text-gray-600 mb-4">The requested log entry could not be found.</p>
                            <button
                                onClick={onBack}
                                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors"
                            >
                                Go Back
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-gray-100 p-4">
            <div className="max-w-4xl mx-auto">
                {/* Header */}
                <div className="bg-white rounded-lg shadow-lg p-6 mb-6">
                    <div className="flex items-center justify-between mb-4">
                        <h1 className="text-2xl font-bold text-gray-800">Log Details</h1>
                        <div className="flex gap-3">
                            <button
                                onClick={() => copyToClipboard(log.message, 'Message')}
                                className="px-4 py-2 text-sm bg-gray-600 text-white rounded hover:bg-gray-700 transition-colors"
                            >
                                Copy Message
                            </button>
                            <button
                                onClick={downloadAsText}
                                className="px-4 py-2 text-sm bg-green-600 text-white rounded hover:bg-green-700 transition-colors"
                            >
                                Download TXT
                            </button>
                        </div>
                    </div>

                    {/* Metadata */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                        <div className="space-y-3">
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">Level</label>
                                <span className={`inline-block text-sm font-mono px-3 py-1 rounded ${getLevelColor(log.levelId)}`}>
                                    {getLevelText(log.levelId)}
                                </span>
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">Source</label>
                                <p className="text-gray-800 font-medium">{log.source}</p>
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">Timestamp</label>
                                <p className="text-gray-800 font-mono text-sm">{formatDate(log.date)}</p>
                            </div>
                        </div>

                        <div className="space-y-3">
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">Unique ID</label>
                                <div className="flex items-center gap-2">
                                    <p className="text-gray-800 font-mono text-sm break-all">{log.uniqueId}</p>
                                    <button
                                        onClick={() => copyToClipboard(log.uniqueId, 'Unique ID')}
                                        className="text-blue-600 hover:text-blue-800 text-xs"
                                    >
                                        Copy
                                    </button>
                                </div>
                            </div>
                            {log.correlationId && (
                                <div>
                                    <label className="block text-sm font-medium text-gray-700 mb-1">Correlation ID</label>
                                    <div className="flex items-center gap-2">
                                        <p className="text-gray-800 font-mono text-sm break-all">{log.correlationId}</p>
                                        <button
                                            onClick={() => copyToClipboard(log.correlationId!, 'Correlation ID')}
                                            className="text-blue-600 hover:text-blue-800 text-xs"
                                        >
                                            Copy
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                {/* Message */}
                <div className="bg-white rounded-lg shadow-lg p-6">
                    <div className="flex items-center justify-between mb-4">
                        <h2 className="text-lg font-semibold text-gray-800">Message</h2>
                        {isJsonFormatted && (
                            <div className="flex items-center gap-2">
                                <span className="text-sm text-gray-600">JSON Format Available</span>
                                <span className="inline-block w-2 h-2 bg-green-500 rounded-full"></span>
                            </div>
                        )}
                    </div>

                    <div className="bg-gray-50 rounded-lg p-4 overflow-auto">
                        <pre className="text-sm text-gray-800 whitespace-pre-wrap break-words">
                            {isJsonFormatted ? formatJsonMessage(log.message) : log.message}
                        </pre>
                    </div>
                </div>
            </div>
        </div>
    );
}