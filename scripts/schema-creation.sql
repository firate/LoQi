-- LoQi Database Schema with Performance Optimizations
-- unique_id => unique like id column
-- correlation_id => for distributed log tracing
-- Level: 0=Verbose, 1=Debug, 2=Information, 3=Warning, 4=Error, 5=Fatal

-- PERFORMANCE OPTIMIZATIONS FIRST
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA cache_size=100000;
PRAGMA temp_store=MEMORY;
PRAGMA wal_autocheckpoint=1000;
PRAGMA page_size=4096;
PRAGMA mmap_size=1073741824;
PRAGMA foreign_keys=OFF;

-- TABLE CREATION
CREATE TABLE IF NOT EXISTS logs
(
    id             INTEGER PRIMARY KEY,
    unique_id      TEXT    NOT NULL,
    correlation_id TEXT,
    redis_stream_id TEXT,
    timestamp      INTEGER NOT NULL,
    level          INTEGER NOT NULL,
    message        TEXT    NOT NULL,
    source         TEXT    NOT NULL
);

-- PERFORMANCE INDEXES
CREATE UNIQUE INDEX IF NOT EXISTS idx_logs_unique_id ON logs(unique_id);
CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_logs_level_timestamp ON logs(level, timestamp DESC) WHERE level >= 3;
CREATE INDEX IF NOT EXISTS idx_logs_correlation_id ON logs(correlation_id) WHERE correlation_id IS NOT NULL;

-- FTS5 for message search
CREATE VIRTUAL TABLE IF NOT EXISTS logs_fts USING fts5(
    message,
    content='logs',
    content_rowid='id',
    tokenize='porter unicode61'
);

-- FTS5 triggers for automatic indexing
CREATE TRIGGER IF NOT EXISTS logs_ai
AFTER INSERT ON logs
BEGIN
INSERT INTO logs_fts(rowid, message) VALUES (new.id, new.message);
END;

CREATE TRIGGER IF NOT EXISTS logs_au
AFTER UPDATE ON logs
BEGIN
UPDATE logs_fts SET message = new.message WHERE rowid = new.id;
END;

CREATE TRIGGER IF NOT EXISTS logs_ad
AFTER DELETE ON logs
BEGIN
DELETE FROM logs_fts WHERE rowid = old.id;
END;

-- Performance monitoring view
CREATE VIEW IF NOT EXISTS performance_stats AS
SELECT 'Database size (MB)' as metric,
       ROUND(page_count * page_size / 1024.0 / 1024.0, 2) as value
FROM pragma_page_count(), pragma_page_size()
UNION ALL
SELECT 'Total logs', COUNT(*) FROM logs
UNION ALL
SELECT 'Logs today', COUNT(*)
FROM logs WHERE timestamp >= strftime('%s', 'now', 'start of day')
UNION ALL
SELECT 'Index count', COUNT(*)
FROM sqlite_master WHERE type = 'index' AND tbl_name = 'logs';
