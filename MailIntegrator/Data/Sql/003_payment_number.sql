-- The payment number becomes the unique business key of a parcel; the tracking number stops being
-- unique, because several parcels may be registered under the same tracking number.
--
-- Rows created before this migration carry no payment number, so the table is rebuilt from scratch and
-- the legacy rows are discarded (documented decision for this iteration). AppSettings is untouched.

DROP TABLE IF EXISTS Parcels;

CREATE TABLE Parcels (
    Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    PaymentNumber           TEXT    NOT NULL,
    TrackId                 TEXT    NOT NULL,
    TrackingServiceCode     TEXT    NULL,
    CreatedDatetimeUtc      TEXT    NOT NULL,
    SentDatetimeUtc         TEXT    NULL,
    ReceivedDatetimeUtc     TEXT    NULL,
    LastCheckedDatetimeUtc  TEXT    NULL,
    LastStatus              TEXT    NULL,
    Comment                 TEXT    NULL,
    IsMigratedTo1CFlag      INTEGER NOT NULL DEFAULT 0,
    MigratedTo1CDatetimeUtc TEXT    NULL
);

-- Business key: unique regardless of casing.
CREATE UNIQUE INDEX IF NOT EXISTS UX_Parcels_PaymentNumber
    ON Parcels (PaymentNumber COLLATE NOCASE);

-- The tracking number is looked up by the status fan-out, so it stays indexed - just not unique.
CREATE INDEX IF NOT EXISTS IX_Parcels_TrackId
    ON Parcels (TrackId COLLATE NOCASE);

-- The grid's default ordering is CreatedDatetime descending.
CREATE INDEX IF NOT EXISTS IX_Parcels_CreatedDatetimeUtc
    ON Parcels (CreatedDatetimeUtc DESC);

CREATE INDEX IF NOT EXISTS IX_Parcels_TrackingServiceCode
    ON Parcels (TrackingServiceCode);
