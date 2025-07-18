
--  unique_id => unique like id column
--  correlation_id => for distributed log tracing
--  Level: 0=Verbose, 1=Debug, 2=Information, 3=Warning, 4=Error, 5=Fatal

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

-- Unique_Id is real key

CREATE UNIQUE INDEX IF NOT EXISTS idx_logs_unique_id ON logs(unique_id);
CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp);
CREATE INDEX IF NOT EXISTS idx_logs_timestamp_level ON logs(timestamp, level);

-- FTS5 for message search
CREATE VIRTUAL TABLE IF NOT EXISTS logs_fts USING fts5(
    message,
    content='logs',
    content_rowid='id'
);

-- FTS5 triggers
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
