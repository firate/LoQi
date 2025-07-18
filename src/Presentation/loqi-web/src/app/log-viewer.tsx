import { useEffect, useState } from "react";
import {
    HubConnection,
    HubConnectionBuilder,
    LogLevel,
} from "@microsoft/signalr";

interface LogEntry {
    id: number;
    message: string;
    level: number;
    source: string;
    timestamp: string;
}

const Page = () => {
    const [connection, setConnection] = useState<HubConnection | null>(null);
    const [connectionStatus, setConnectionStatus] = useState<'Connected' | 'Disconnected' | 'Error'>('Disconnected');
    const [logs, setLogs] = useState<LogEntry[]>([]);

    // Bağlantıyı oluştur
    useEffect(() => {
        const newConnection = new HubConnectionBuilder()
            .withUrl("http://localhost:5003/loghub") // 🔁
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Information)
            .build();

        setConnection(newConnection);
    }, []);

    // Bağlantıyı başlat ve event handler'ı ekle
    useEffect(() => {
        let isMounted = true;

        if (connection) {
            connection
                .start()
                .then(() => {
                    console.log("✅ SignalR connected");
                    setConnectionStatus('Connected');

                    if (isMounted) {
                        connection.off("NewLogEntry"); // Daha önce eklenmişse temizle
                        connection.on("NewLogEntry", (data) => {
                            console.log("📩 New log received:", data);
                            setLogs((prevLogs) => [...prevLogs, data]);
                        });
                    }
                })
                .catch((err) => {
                    console.error("🛑 SignalR Connection Error:", err);
                    setConnectionStatus('Error');
                });


            connection.onclose(() => {
                setConnectionStatus('Disconnected');
            });
        }

        return () => {
            isMounted = false;
            connection?.stop();
        };
    }, [connection]);

    const getLevelColor = (level: number) => {
        switch (level) {
            case 0: return 'text-gray-600'; // Trace
            case 1: return 'text-blue-600'; // Debug
            case 2: return 'text-green-600'; // Info
            case 3: return 'text-yellow-600'; // Warning
            case 4: return 'text-red-600'; // Error
            case 5: return 'text-red-800'; // Critical
            default: return 'text-gray-600';
        }
    };

    const getLevelText = (level: number) => {
        const levels = ['TRACE', 'DEBUG', 'INFO', 'WARN', 'ERROR', 'CRITICAL'];
        return levels[level] || 'UNKNOWN';
    };

    const clearLogs = () => {
        setLogs([]);
    };

    return (
        <div className="min-h-screen bg-gray-100 p-4">
        <div className="max-w-6xl mx-auto">
        <div className="bg-white rounded-lg shadow-lg p-6">
            {/* Header */}
            <div className="flex justify-between items-center mb-6">
    <h1 className="text-2xl font-bold text-gray-800">Log Viewer</h1>
    <div className="flex items-center gap-4">
    <div className="flex items-center gap-2">
    <div className={`w-3 h-3 rounded-full ${
        connectionStatus === 'Connected' ? 'bg-green-500' :
            connectionStatus === 'Error' ? 'bg-red-500' : 'bg-gray-500'
    }`}></div>
    <span className="text-sm text-gray-600">{connectionStatus}</span>
        </div>
        <button
    onClick={clearLogs}
    className="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600 transition-colors"
        >
        Clear Logs
    </button>
    </div>
    </div>

    {/* Log Count */}
    <div className="mb-4">
    <p className="text-sm text-gray-600">
        {logs.length} log{logs.length !== 1 ? 's' : ''} displayed
    </p>
    </div>

    {/* Logs List */}
    <div className="space-y-2 max-h-96 overflow-y-auto">
        {
            logs.length === 0 ? 
            (
                <div className="text-center py-8 text-gray-500">
                    No logs received yet. Waiting for new log entries...
                </div>
            ) : 
            (
                logs.map((log, index) => 
                    (
                        <div key={`${log.timestamp}-${index}`} className="bg-gray-50 rounded p-3 border-l-4 border-gray-300">
                            <div className="flex items-start justify-between">
                                <div className="flex-1">
                                    <div className="flex items-center gap-3 mb-1">
                                        <span className={`font-mono text-xs px-2 py-1 rounded ${getLevelColor(log.level)} bg-white`}>
                                            {getLevelText(log.level)}
                                        </span>
                                        <span className="text-sm text-gray-600 font-medium">{log.source}</span>
                                        <span className="text-xs text-gray-400">{new Date(log.timestamp).toLocaleString()}</span>
                                    </div>
                                    <p className="text-gray-800 break-words">{log.message}</p>
                                </div>
                            </div>
                        </div>
                    )
                )
            )
        }
    </div>
    </div>
    </div>
    </div>
);
};

export default Page;