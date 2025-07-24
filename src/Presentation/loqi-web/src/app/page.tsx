"use client";
import { useState } from 'react';
import LogViewer from '../components/LogViewer';
import LogSearchPage from '../components/LogSearchPage';
import Logo from '../components/Logo';

export default function Home() {
    const [activeTab, setActiveTab] = useState<'live' | 'search'>('live');

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