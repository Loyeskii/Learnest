namespace Learnest.Data
{
    /// <summary>Public information about the SQLite engine Learnest is running on.</summary>
    public static class SqliteInfo
    {
        /// <summary>"winsqlite3.dll 3.43.2" or "sqlite3.dll 3.53.1".</summary>
        public static string Describe()
        {
            string error;
            if (!Sqlite3.Probe(out error)) { return "(unavailable) " + error; }
            return Sqlite3.LibraryInUse + " " + Sqlite3.Version;
        }

        public static bool IsAvailable(out string error)
        {
            return Sqlite3.Probe(out error);
        }

        public static string LibraryName
        {
            get
            {
                string error;
                Sqlite3.Probe(out error);
                return Sqlite3.LibraryInUse;
            }
        }

        public static string Version
        {
            get
            {
                string error;
                Sqlite3.Probe(out error);
                return Sqlite3.Version;
            }
        }
    }
}
