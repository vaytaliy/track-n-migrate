-- Tracking provider association and the generic status enum, stored by name.
-- Existing rows predate providers, so TrackingServiceCode stays NULL for them.

ALTER TABLE Parcels ADD COLUMN TrackingServiceCode TEXT NULL;

CREATE INDEX IF NOT EXISTS IX_Parcels_TrackingServiceCode
    ON Parcels (TrackingServiceCode);

-- Normalise the previous free-text statuses onto the generic enum.
-- Values that cannot be mapped become 'Unknown', which the view renders as "?".
UPDATE Parcels
SET LastStatus = CASE TRIM(LastStatus)
    WHEN 'Доставлено'       THEN 'Delivered'
    WHEN 'В пути'           THEN 'InTransit'
    WHEN 'Ожидает отправки' THEN 'Processing'
    WHEN ''                 THEN NULL
    ELSE 'Unknown'
END
WHERE LastStatus IS NOT NULL;
