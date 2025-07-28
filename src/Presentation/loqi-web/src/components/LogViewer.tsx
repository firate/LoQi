// LogViewer.tsx - Smart version with live mode toggle
import {useEffect, useState, useRef, useCallback} from "react";
import {
    HubConnection,
    HubConnectionBuilder,
    LogLevel,
    HubConnectionState,
} from "@microsoft/signalr";
import {LogEntry} from "../types/log";

const LogViewer = () => {
    const [connection, setConnection] = useState<HubConnection | null>(null);
    const [connectionStatus, setConnectionStatus] = useState<'Connected' | 'Connecting' | 'Disconnected' | 'Error'>('Disconnected');
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [maxLogs, setMaxLogs] = useState<number>(10);
    const [isLiveMode, setIsLiveMode] = useState<boolean>(false); // 🔥 Live mode control

    const maxLogsRef = useRef<number>(10);
    const connectionRef = useRef<HubConnection | null>(null);

    // ✅ Connection oluşturma
    const createConnection = useCallback(() => {
        const newConnection = new HubConnectionBuilder()
            .withUrl("http://localhost:5003/loghub")
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Information)
            .build();

        // Connection event handlers
        newConnection.onreconnecting(() => {
            console.log("🔄 SignalR reconnecting...");
            setConnectionStatus('Connecting');
        });

        newConnection.onreconnected(() => {
            console.log("✅ SignalR reconnected");
            setConnectionStatus('Connected');

            // Re-join live logs group if live mode was active
            if (isLiveMode) {
                joinLiveLogsGroup(newConnection);
            }
        });

        newConnection.onclose((error) => {
            console.log(" SignalR connection closed", error);
            setConnectionStatus('Disconnected');
            connectionRef.current = null;
        });

        return newConnection;
    }, [isLiveMode]);

    // ✅ Live Logs grubuna katılma
    const joinLiveLogsGroup = useCallback(async (conn: HubConnection) => {
        try {
            if (conn.state === HubConnectionState.Connected) {
                await conn.invoke("JoinLiveLogsGroup");
                console.log("Joined live logs group");
            }
        } catch (error) {
            console.error("Failed to join live logs group:", error);
        }
    }, []);

    // ✅ Live Logs grubundan çıkma
    const leaveLiveLogsGroup = useCallback(async (conn: HubConnection) => {
        try {
            if (conn.state === HubConnectionState.Connected) {
                await conn.invoke("LeaveLiveLogsGroup");
                console.log("Left live logs group");
            }
        } catch (error) {
            console.error("Failed to leave live logs group:", error);
        }
    }, []);

    // ✅ Connection başlatma
    const startConnection = useCallback(async () => {
        if (connectionRef.current?.state === HubConnectionState.Connected) {
            return;
        }

        setConnectionStatus('Connecting');

        try {
            const newConnection = createConnection();
            connectionRef.current = newConnection;
            setConnection(newConnection);

            await newConnection.start();
            console.log("SignalR connected successfully");
            setConnectionStatus('Connected');

            // Event handler'ı ekle
            newConnection.on("NewLogEntry", (data) => {
                console.log("New log received:", data);
                setLogs((prevLogs) => {
                    const newLogs = [...prevLogs, data];
                    return newLogs.slice(-maxLogsRef.current);
                });
            });

            // Live mode aktifse gruba katıl
            if (isLiveMode) {
                await joinLiveLogsGroup(newConnection);
            }

        } catch (error) {
            console.error("SignalR connection failed:", error);
            setConnectionStatus('Error');
            connectionRef.current = null;
        }
    }, [createConnection, isLiveMode, joinLiveLogsGroup]);

    // ✅ Connection durdurma
    const stopConnection = useCallback(async () => {
        if (connectionRef.current) {
            try {
                if (isLiveMode && connectionRef.current.state === HubConnectionState.Connected) {
                    await leaveLiveLogsGroup(connectionRef.current);
                }

                await connectionRef.current.stop();
                console.log("SignalR connection stopped");
            } catch (error) {
                console.error("Error stopping connection:", error);
            } finally {
                connectionRef.current = null;
                setConnection(null);
                setConnectionStatus('Disconnected');
            }
        }
    }, [isLiveMode, leaveLiveLogsGroup]);

    // ✅ Live mode toggle
    const toggleLiveMode = useCallback(async () => {
        const newLiveMode = !isLiveMode;
        setIsLiveMode(newLiveMode);

        if (connectionRef.current?.state === HubConnectionState.Connected) {
            if (newLiveMode) {
                await joinLiveLogsGroup(connectionRef.current);
            } else {
                await leaveLiveLogsGroup(connectionRef.current);
            }
        } else if (newLiveMode) {
            // Live mode açılıyorsa connection başlat
            await startConnection();
        }
    }, [isLiveMode, joinLiveLogsGroup, leaveLiveLogsGroup, startConnection]);

    // ✅ Component lifecycle
    useEffect(() => {
        maxLogsRef.current = maxLogs;
    }, [maxLogs]);

    useEffect(() => {
        // Component mount edildiğinde connection başlatma
        // Sadece live mode açıksa başlat
        if (isLiveMode) {
            startConnection();
        }

        // Cleanup
        return () => {
            stopConnection();
        };
    }, []); // Sadece mount/unmount'ta çalışsın

    // ✅ Live mode değişikliklerini handle et
    useEffect(() => {
        if (isLiveMode && !connectionRef.current) {
            startConnection();
        } else if (!isLiveMode && connectionRef.current) {
            // Live mode kapatıldığında sadece gruptan çık, connection'ı kesme
            if (connectionRef.current.state === HubConnectionState.Connected) {
                leaveLiveLogsGroup(connectionRef.current);
            }
        }
    }, [isLiveMode, startConnection, leaveLiveLogsGroup]);

    // ✅ Utility functions
    const getLevelColor = (level: number) => {
        switch (level) {
            case 0:
                return 'text-gray-600';
            case 1:
                return 'text-blue-600';
            case 2:
                return 'text-green-600';
            case 3:
                return 'text-yellow-600';
            case 4:
                return 'text-red-600';
            case 5:
                return 'text-red-800';
            default:
                return 'text-gray-600';
        }
    };

    const getLevelText = (level: number) => {
        const levels = ['TRACE', 'DEBUG', 'INFO', 'WARN', 'ERROR', 'CRITICAL'];
        return levels[level] || 'UNKNOWN';
    };

    const clearLogs = () => {
        setLogs([]);
    };

    const changeMaxLogs = (newLimit: number) => {
        setMaxLogs(newLimit);
        maxLogsRef.current = newLimit;
        setLogs(prevLogs => prevLogs.slice(-newLimit));
    };

    // ✅ Connection status indicator
    const getConnectionStatusColor = () => {
        if (!isLiveMode) return 'bg-gray-500';

        switch (connectionStatus) {
            case 'Connected':
                return 'bg-green-500';
            case 'Connecting':
                return 'bg-yellow-500';
            case 'Error':
                return 'bg-red-500';
            default:
                return 'bg-gray-500';
        }
    };

    const getConnectionStatusText = () => {
        if (!isLiveMode) return 'Live Mode Off';
        return connectionStatus;
    };

    return (
        <div className="min-h-screen bg-gray-100 p-4">
            <div className="max-w-6xl mx-auto">
                <div className="bg-white rounded-lg shadow-lg p-6">

                    {/* Header with Live Mode Toggle */}
                    <div className="flex justify-between items-center mb-6">
                        <div className="flex items-center gap-4">
                            <h1 className="text-2xl font-bold text-gray-800">
                                {isLiveMode ? `Live Logs (Last ${maxLogs})` : 'Log Viewer (Paused)'}
                            </h1>

                            {/* ✅ EKLE: Live Mode Toggle */}
                            <button
                                onClick={toggleLiveMode}
                                className={`px-4 py-2 rounded-lg font-medium transition-colors ${
                                    isLiveMode
                                        ? 'bg-green-500 text-white hover:bg-green-600'
                                        : 'bg-gray-500 text-white hover:bg-gray-600'
                                }`}
                            >
                                {isLiveMode ? '🟢 Live' : '⏸️ Paused'}
                            </button>
                        </div>

                        <div className="flex items-center gap-4">
                            {/* ✅ GÜNCELLE: Connection Status */}
                            <div className="flex items-center gap-2">
                                <div className={`w-3 h-3 rounded-full ${getConnectionStatusColor()}`}></div>
                                <span className="text-sm text-gray-600">{getConnectionStatusText()}</span>
                            </div>

                            <button
                                onClick={clearLogs}
                                className="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600 transition-colors"
                            >
                                Clear Logs
                            </button>
                        </div>
                    </div>

                    {/* ✅ EKLE: Performance Info */}
                    <div className="mb-4 p-3 bg-blue-50 rounded-lg">
                        <p className="text-sm text-blue-800">
                            💡 <strong>Smart Notifications:</strong> {
                            isLiveMode
                                ? "Real-time notifications are active. Server is sending live updates."
                                : "Notifications are paused. Server is not sending updates to save resources."
                        }
                        </p>
                    </div>


                    {/* Log Count */}
                    <div className="mb-4">
                        <p className="text-sm text-gray-600">
                            {logs.length} log{logs.length !== 1 ? 's' : ''} displayed
                            {isLiveMode && ` (showing last ${maxLogs})`}
                        </p>
                    </div>

                    {/* Logs List */}
                    <div className="space-y-2 max-h-96 overflow-y-auto">
                        {logs.length === 0 ? (
                            <div className="text-center py-8 text-gray-500">
                                {isLiveMode ? (
                                    <>
                                        <div className="mb-2"></div>
                                        <p>Waiting for new log entries...</p>
                                        <p className="text-xs mt-1">
                                            Status: {getConnectionStatusText()}
                                        </p>
                                    </>
                                ) : (
                                    <>
                                        <div className="mb-2"></div>
                                        <p>Live mode is paused.</p>
                                        <p className="text-xs mt-1">
                                            Click "Live" button to start receiving real-time logs.
                                        </p>
                                    </>
                                )}
                            </div>
                        ) : (
                            logs.map((log, index) => (
                                <div key={`${log.timestamp}-${index}`}
                                     className="bg-gray-50 rounded p-3 border-l-4 border-gray-300">
                                    <div className="flex items-start justify-between">
                                        <div className="flex-1">
                                            <div className="flex items-center gap-3 mb-1">
                                                <span
                                                    className={`font-mono text-xs px-2 py-1 rounded ${getLevelColor(log.level)} bg-white`}>
                                                    {getLevelText(log.level)}
                                                </span>
                                                <span className="text-sm text-gray-600 font-medium">
                                                    {log.source}
                                                </span>
                                                <span className="text-xs text-gray-400">
                                                    {new Date(log.timestamp).toLocaleString()}
                                                </span>
                                            </div>
                                            <p className="text-gray-800 break-words">{log.message}</p>
                                        </div>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LogViewer;