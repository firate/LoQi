-- LoQi Database Schema with Performance Optimizations
-- unique_id => unique like id column
-- correlation_id => for distributed log tracing
-- Level: 0=Verbose, 1=Debug, 2=Information, 3=Warning, 4=Error, 5=Fatal

-- PERFORMANCE OPTIMIZATIONS FIRST
-- Enable WAL mode for better concurrency
PRAGMA journal_mode=WAL;

-- Optimize synchronization for performance  
PRAGMA synchronous=NORMAL;  -- Instead of FULL (default)

-- Increase cache size (100MB cache)
PRAGMA cache_size=100000;   -- 100K pages * 1KB = ~100MB

-- Use memory for temporary storage
PRAGMA temp_store=MEMORY;

-- Optimize WAL autocheckpoint
PRAGMA wal_autocheckpoint=1000;  -- Checkpoint every 1000 pages

-- Optimize page size for logging workload
PRAGMA page_size=4096;  -- 4KB pages (good for text data)

-- Enable memory-mapped I/O (1GB)
PRAGMA mmap_size=1073741824;

-- Optimize foreign key checks (disable if not using foreign keys)
PRAGMA foreign_keys=OFF;

--  TABLE CREATION
CREATE TABLE IF NOT EXISTS logs (
                                    id INTEGER PRIMARY KEY,
                                    unique_id TEXT NOT NULL,
                                    correlation_id TEXT,
                                    timestamp INTEGER NOT NULL,
                                    offset_minutes INTEGER NOT NULL,
                                    level INTEGER NOT NULL,
                                    message TEXT NOT NULL,
                                    source TEXT NOT NULL
);

--  PERFORMANCE INDEXES
-- Unique_Id is real key
CREATE UNIQUE INDEX IF NOT EXISTS idx_logs_unique_id ON logs(unique_id);

-- Basic timestamp index
CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp);

-- Composite indexes for common filtering scenarios
CREATE INDEX IF NOT EXISTS idx_logs_timestamp_level ON logs(timestamp, level);
CREATE INDEX IF NOT EXISTS idx_logs_source_timestamp ON logs(source, timestamp);
CREATE INDEX IF NOT EXISTS idx_logs_level_timestamp ON logs(level, timestamp);
CREATE INDEX IF NOT EXISTS idx_logs_correlation_id ON logs(correlation_id);

-- High-performance composite index for complex queries
CREATE INDEX IF NOT EXISTS idx_logs_timestamp_level_source ON logs(timestamp, level, source);

--  FTS5 for message search
CREATE VIRTUAL TABLE IF NOT EXISTS logs_fts USING fts5(
    message,
    content='logs',
    content_rowid='id'
);

--  FTS5 triggers for automatic indexing
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

--  VERIFICATION - Display current configuration
SELECT
    'SQLite Version' as setting,
    sqlite_version() as value
UNION ALL
SELECT
    'journal_mode',
    (SELECT value FROM pragma_journal_mode())
UNION ALL
SELECT
    'synchronous',
    (SELECT value FROM pragma_synchronous())
UNION ALL
SELECT
    'cache_size',
    (SELECT value FROM pragma_cache_size())
UNION ALL
SELECT
    'temp_store',
    (SELECT value FROM pragma_temp_store())
UNION ALL
SELECT
    'page_size',
    (SELECT value FROM pragma_page_size())
UNION ALL
SELECT
    'mmap_size',
    (SELECT value FROM pragma_mmap_size());

--  Performance monitoring view
CREATE VIEW IF NOT EXISTS performance_stats AS
SELECT
    'Database size (MB)' as metric,
    ROUND(page_count * page_size / 1024.0 / 1024.0, 2) as value
FROM pragma_page_count(), pragma_page_size()
UNION ALL
SELECT
    'Total logs',
    COUNT(*)
FROM logs
UNION ALL
SELECT
    'Logs today',
    COUNT(*)
FROM logs
WHERE timestamp >= strftime('%s', 'now', 'start of day')
UNION ALL
SELECT
    'Index count',
    COUNT(*)
FROM sqlite_master
WHERE type = 'index' AND tbl_name = 'logs';

-- Display initial stats
SELECT * FROM performance_stats;