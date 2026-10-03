using Dapper;
using MailIntegrator.Models;

namespace MailIntegrator.Data;

/// <summary>
/// Dapper based SQLite implementation of <see cref="IParcelRepository"/>.
/// </summary>
public sealed class ParcelRepository : IParcelRepository
{
    private const string SelectColumns = """
        Id,
        TrackId,
        TrackingServiceCode,
        CreatedDatetimeUtc,
        SentDatetimeUtc,
        ReceivedDatetimeUtc,
        LastCheckedDatetimeUtc,
        LastStatus,
        Comment,
        IsMigratedTo1CFlag,
        MigratedTo1CDatetimeUtc
        """;

    private const string InsertSql = """
        INSERT INTO Parcels (
            TrackId,
            TrackingServiceCode,
            CreatedDatetimeUtc,
            SentDatetimeUtc,
            ReceivedDatetimeUtc,
            LastCheckedDatetimeUtc,
            LastStatus,
            Comment,
            IsMigratedTo1CFlag,
            MigratedTo1CDatetimeUtc)
        VALUES (
            $trackId,
            $trackingServiceCode,
            $createdDatetimeUtc,
            $sentDatetimeUtc,
            $receivedDatetimeUtc,
            $lastCheckedDatetimeUtc,
            $lastStatus,
            $comment,
            $isMigratedTo1CFlag,
            $migratedTo1CDatetimeUtc);
        SELECT last_insert_rowid();
        """;

    private const string UpdateSql = """
        UPDATE Parcels SET
            TrackId                 = $trackId,
            SentDatetimeUtc         = $sentDatetimeUtc,
            ReceivedDatetimeUtc     = $receivedDatetimeUtc,
            LastCheckedDatetimeUtc  = $lastCheckedDatetimeUtc,
            LastStatus              = $lastStatus,
            Comment                 = $comment,
            IsMigratedTo1CFlag      = $isMigratedTo1CFlag,
            MigratedTo1CDatetimeUtc = $migratedTo1CDatetimeUtc
        WHERE Id = $id;
        """;

    private readonly AppDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelRepository"/> class.
    /// </summary>
    /// <param name="database">The database that supplies connections.</param>
    public ParcelRepository(AppDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <inheritdoc />
    public IReadOnlyList<Parcel> GetAll()
    {
        using var connection = _database.OpenConnection();
        var parcels = connection.Query<Parcel>($"""
            SELECT {SelectColumns}
            FROM Parcels
            ORDER BY CreatedDatetimeUtc DESC, Id DESC;
            """);

        return parcels.AsList();
    }

    /// <inheritdoc />
    public Parcel? GetById(long id)
    {
        using var connection = _database.OpenConnection();
        return connection.QuerySingleOrDefault<Parcel>(
            $"SELECT {SelectColumns} FROM Parcels WHERE Id = $id;",
            new { id });
    }

    /// <inheritdoc />
    public long Insert(Parcel parcel)
    {
        ArgumentNullException.ThrowIfNull(parcel);

        using var connection = _database.OpenConnection();
        var id = connection.ExecuteScalar<long>(InsertSql, new
        {
            trackId = parcel.TrackId,
            trackingServiceCode = parcel.TrackingServiceCode,
            createdDatetimeUtc = parcel.CreatedDatetimeUtc,
            sentDatetimeUtc = parcel.SentDatetimeUtc,
            receivedDatetimeUtc = parcel.ReceivedDatetimeUtc,
            lastCheckedDatetimeUtc = parcel.LastCheckedDatetimeUtc,
            lastStatus = parcel.LastStatus?.ToString(),
            comment = parcel.Comment,
            isMigratedTo1CFlag = parcel.IsMigratedTo1CFlag,
            migratedTo1CDatetimeUtc = parcel.MigratedTo1CDatetimeUtc,
        });

        parcel.Id = id;
        return id;
    }

    /// <inheritdoc />
    public bool Update(Parcel parcel)
    {
        ArgumentNullException.ThrowIfNull(parcel);

        using var connection = _database.OpenConnection();
        var affected = connection.Execute(UpdateSql, new
        {
            id = parcel.Id,
            trackId = parcel.TrackId,
            sentDatetimeUtc = parcel.SentDatetimeUtc,
            receivedDatetimeUtc = parcel.ReceivedDatetimeUtc,
            lastCheckedDatetimeUtc = parcel.LastCheckedDatetimeUtc,
            lastStatus = parcel.LastStatus?.ToString(),
            comment = parcel.Comment,
            isMigratedTo1CFlag = parcel.IsMigratedTo1CFlag,
            migratedTo1CDatetimeUtc = parcel.MigratedTo1CDatetimeUtc,
        });

        return affected > 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The <c>IsMigratedTo1CFlag = 0</c> predicate enforces the global lock rule at the storage layer,
    /// so a migrated parcel cannot be removed even if a caller bypasses the service.
    /// </remarks>
    public bool Delete(long id)
    {
        using var connection = _database.OpenConnection();
        var affected = connection.Execute(
            "DELETE FROM Parcels WHERE Id = $id AND IsMigratedTo1CFlag = 0;",
            new { id });

        return affected > 0;
    }

    /// <inheritdoc />
    public bool TrackIdExists(string trackId, long? excludingId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

        using var connection = _database.OpenConnection();
        return connection.ExecuteScalar<bool>("""
            SELECT EXISTS (
                SELECT 1
                FROM Parcels
                WHERE TrackId = $trackId COLLATE NOCASE
                  AND ($excludingId IS NULL OR Id <> $excludingId)
            );
            """,
            new { trackId = trackId.Trim(), excludingId });
    }
}
