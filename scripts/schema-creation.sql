
--  unique_id => unique like id column
--  correlation_id => for distributed log tracing
 

CREATE TABLE IF NOT EXISTS logs (
    id INTEGER PRIMARY KEY,
    unique_id TEXT NOT NULL,
    correlation_id TEXT,
    timestamp INTEGER NOT NULL,
    offset_minutes INTEGER NOT NULL,
    level TEXT NOT NULL,
    message TEXT NOT NULL,
    source TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp);
CREATE INDEX IF NOT EXISTS idx_logs_level ON logs(level);
CREATE INDEX IF NOT EXISTS idx_logs_timestamp_level ON logs(timestamp, level);

CREATE VIRTUAL TABLE IF NOT EXISTS logs_fts USING fts5(
    message,
    content='logs',
    content_rowid='id'
);

-- INSERT trigger
CREATE TRIGGER IF NOT EXISTS logs_ai 
AFTER INSERT ON logs
BEGIN
INSERT INTO logs_fts(rowid, message) VALUES (new.id, new.message);
END;

-- UPDATE trigger
CREATE TRIGGER IF NOT EXISTS logs_au 
AFTER UPDATE ON logs
BEGIN
UPDATE logs_fts SET message = new.message WHERE rowid = new.id;
END;

-- DELETE trigger
CREATE TRIGGER IF NOT EXISTS logs_ad 
AFTER DELETE ON logs
BEGIN
DELETE FROM logs_fts WHERE rowid = old.id;
END;