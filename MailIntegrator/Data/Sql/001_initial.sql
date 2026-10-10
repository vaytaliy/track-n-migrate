-- Initial schema for the mail integrator skeleton.
-- All DateTime columns are stored as explicit ISO-8601 UTC text (see DateTimeStorage).

CREATE TABLE IF NOT EXISTS Parcels (
    Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    TrackId                 TEXT    NOT NULL,
    CreatedDatetimeUtc      TEXT    NOT NULL,
    SentDatetimeUtc         TEXT    NULL,
    ReceivedDatetimeUtc     TEXT    NULL,
    LastCheckedDatetimeUtc  TEXT    NULL,
    LastStatus              TEXT    NULL,
    Comment                 TEXT    NULL,
    IsMigratedTo1CFlag      INTEGER NOT NULL DEFAULT 0,
    MigratedTo1CDatetimeUtc TEXT    NULL
);

-- Tracking numbers are unique regardless of casing.
CREATE UNIQUE INDEX IF NOT EXISTS UX_Parcels_TrackId
    ON Parcels (TrackId COLLATE NOCASE);

-- The grid's default ordering is CreatedDatetime descending.
CREATE INDEX IF NOT EXISTS IX_Parcels_CreatedDatetimeUtc
    ON Parcels (CreatedDatetimeUtc DESC);

-- Non-secret application settings only. Credentials live in Windows Credential Manager.
CREATE TABLE IF NOT EXISTS AppSettings (
    Key          TEXT PRIMARY KEY,
    Value        TEXT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
