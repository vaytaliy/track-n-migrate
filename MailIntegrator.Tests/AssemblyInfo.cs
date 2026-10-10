using Xunit;

// Every test class that touches storage shares one process-wide SQLite connection pool, and
// TempDatabase.Dispose clears that pool to unlock its file. Running the classes in parallel therefore
// makes one test dispose connections that another test is still using, which surfaces as random
// "Cannot access a disposed object: SQLitePCL.sqlite3" failures. The suite is fast enough to run
// sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
