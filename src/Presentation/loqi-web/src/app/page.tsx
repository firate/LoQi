"use client";
import { useState, useEffect } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import LogViewer from '../components/LogViewer';
import LogSearchPage from '../components/LogSearchPage';
import LogDetailSection from '../components/LogDetailSection';
import Logo from '../components/Logo';

export default function Home() {
    const [activeTab, setActiveTab] = useState<'live' | 'search'>('live');
    const searchParams = useSearchParams();
    const router = useRouter();
    const logId = searchParams.get('logId');

    // Handle back navigation from log detail
    const handleBackFromDetail = () => {
        router.push('/');
    };

    // If logId exists in URL, show log detail
    if (logId) {
        return (
            <div>
                <div className="bg-white shadow-sm border-b">
                    <div className="max-w-7xl mx-auto px-4">
                        <div className="flex items-center justify-between py-4">
                            <Logo />
                            <button
                                onClick={handleBackFromDetail}
                                className="px-4 py-2 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors"
                            >
                                ← Back to Search
                            </button>
                        </div>
                    </div>
                </div>
                <LogDetailSection uniqueId={logId} onBack={handleBackFromDetail} />
            </div>
        );
    }

    // Normal homepage with tabs
    return (
        <div>
            <div className="bg-white shadow-sm border-b">
                <div className="max-w-7xl mx-auto px-4">
                    <div className="flex items-center justify-between py-4">
                        <Logo />
                        <nav className="flex space-x-8">
                            <button
                                onClick={() => setActiveTab('live')}
                                className={`px-3 py-2 text-sm font-medium rounded-md transition-colors ${
                                    activeTab === 'live'
                                        ? 'bg-blue-100 text-blue-700'
                                        : 'text-gray-500 hover:text-gray-700'
                                }`}
                            >
                                Live Logs
                            </button>
                            <button
                                onClick={() => setActiveTab('search')}
                                className={`px-3 py-2 text-sm font-medium rounded-md transition-colors ${
                                    activeTab === 'search'
                                        ? 'bg-blue-100 text-blue-700'
                                        : 'text-gray-500 hover:text-gray-700'
                                }`}
                            >
                                Search Logs
                            </button>
                        </nav>
                    </div>
                </div>
            </div>

            <div>
                {activeTab === 'live' && <LogViewer />}
                {activeTab === 'search' && <LogSearchPage />}
            </div>
        </div>
    );
}