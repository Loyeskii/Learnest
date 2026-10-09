using System;
using System.Collections;
using System.Collections.Generic;
using Learnest.Core;

namespace Learnest.Data
{
    /// <summary>An open SQLite connection.  One instance is owned by the container session.</summary>
    public sealed class SqliteDatabase : IDisposable
    {
        private IntPtr _db = IntPtr.Zero;
        private readonly object _sync = new object();

        public string Path { get; private set; }
        public string NativeLibrary { get { return Sqlite3.LibraryInUse; } }
        public string NativeVersion { get { return Sqlite3.Version; } }
        public bool IsOpen { get { return _db != IntPtr.Zero; } }

        private SqliteDatabase(IntPtr db, string path)
        {
            _db = db;
            Path = path;
        }

        public static SqliteDatabase Open(string path)
        {
            string probeError;
            if (!Sqlite3.Probe(out probeError))
            {
                throw new SqliteException(
                    "No usable SQLite library was found. Expected either an app-local sqlite3.dll " +
                    "or the Windows system winsqlite3.dll (Windows 10 1607+). Details: " + probeError, -1);
            }

            IntPtr db;
            IntPtr name = Utf8.Alloc(path);
            int rc;
            try
            {
                rc = Sqlite3.Open(name, out db);
            }
            finally
            {
                Utf8.Free(name);
            }
            if (rc != Sqlite3.Ok)
            {
                throw new SqliteException("Unable to open the container database (" + path + "), sqlite rc=" + rc, rc);
            }

            SqliteDatabase database = new SqliteDatabase(db, path);
            database.ApplyPragmas();
            return database;
        }

        private void ApplyPragmas()
        {
            // WAL keeps the UI responsive and survives power loss; NORMAL sync is the standard
            // trade-off for a local desktop database and is safe with WAL.
            Execute("PRAGMA journal_mode=WAL;");
            Execute("PRAGMA synchronous=NORMAL;");
            Execute("PRAGMA foreign_keys=ON;");
            Execute("PRAGMA busy_timeout=5000;");
            Execute("PRAGMA temp_store=MEMORY;");
        }

        internal IntPtr Handle { get { return _db; } }

        public void Execute(string sql)
        {
            lock (_sync)
            {
                EnsureOpen();
                IntPtr error;
                IntPtr statement = Utf8.Alloc(sql);
                int rc;
                try
                {
                    rc = Sqlite3.Exec(_db, statement, out error);
                }
                finally
                {
                    Utf8.Free(statement);
                }
                if (rc != Sqlite3.Ok)
                {
                    string message = error != IntPtr.Zero ? Utf8.Read(error) : Sqlite3.ErrorMessage(_db);
                    throw new SqliteException("SQL failed: " + message + " [" + Truncate(sql) + "]", rc);
                }
            }
        }

        private static string Truncate(string sql)
        {
            if (sql == null) { return ""; }
            string flat = sql.Replace("\r", " ").Replace("\n", " ");
            return flat.Length <= 160 ? flat : flat.Substring(0, 160) + "...";
        }

        public SqliteStatement Prepare(string sql)
        {
            lock (_sync)
            {
                EnsureOpen();
                IntPtr raw;
                IntPtr text = Utf8.Alloc(sql);
                int rc;
                try
                {
                    rc = Sqlite3.Prepare(_db, text, out raw);
                }
                finally
                {
                    Utf8.Free(text);
                }
                if (rc != Sqlite3.Ok)
                {
                    throw new SqliteException("Unable to prepare SQL: " + Sqlite3.ErrorMessage(_db) + " [" + Truncate(sql) + "]", rc);
                }
                return new SqliteStatement(this, raw, sql);
            }
        }

        /// <summary>Run a statement that returns no rows and report affected rows.</summary>
        public int ExecuteNonQuery(string sql, params object[] parameters)
        {
            using (SqliteStatement statement = Prepare(sql))
            {
                statement.BindAll(parameters);
                return statement.RunToCompletion();
            }
        }

        public long ExecuteScalarLong(string sql, params object[] parameters)
        {
            using (SqliteStatement statement = Prepare(sql))
            {
                statement.BindAll(parameters);
                if (!statement.Step()) { return 0; }
                if (statement.IsNull(0)) { return 0; }
                return statement.GetInt64(0);
            }
        }

        public string ExecuteScalarString(string sql, params object[] parameters)
        {
            using (SqliteStatement statement = Prepare(sql))
            {
                statement.BindAll(parameters);
                if (!statement.Step()) { return null; }
                if (statement.IsNull(0)) { return null; }
                return statement.GetString(0);
            }
        }

        public double ExecuteScalarDouble(string sql, params object[] parameters)
        {
            using (SqliteStatement statement = Prepare(sql))
            {
                statement.BindAll(parameters);
                if (!statement.Step()) { return 0; }
                if (statement.IsNull(0)) { return 0; }
                return statement.GetDouble(0);
            }
        }

        /// <summary>Materialise a result set as a list of dictionaries (small result sets only).</summary>
        public List<Dictionary<string, object>> Query(string sql, params object[] parameters)
        {
            List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
            using (SqliteStatement statement = Prepare(sql))
            {
                statement.BindAll(parameters);
                while (statement.Step())
                {
                    Dictionary<string, object> row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < statement.ColumnCount; i++)
                    {
                        row[statement.ColumnName(i)] = statement.GetValue(i);
                    }
                    rows.Add(row);
                }
            }
            return rows;
        }

        public long LastInsertRowId()
        {
            lock (_sync)
            {
                EnsureOpen();
                return Sqlite3.LastInsertRowId(_db);
            }
        }

        public int Changes()
        {
            lock (_sync)
            {
                EnsureOpen();
                return Sqlite3.Changes(_db);
            }
        }

        public void BeginTransaction()
        {
            Execute("BEGIN IMMEDIATE;");
        }

        public void Commit()
        {
            Execute("COMMIT;");
        }

        public void Rollback()
        {
            try { Execute("ROLLBACK;"); }
            catch (SqliteException) { }
        }

        /// <summary>Run <paramref name="action"/> inside a transaction, rolling back on error.</summary>
        public void Transaction(Action action)
        {
            BeginTransaction();
            try
            {
                action();
                Commit();
            }
            catch
            {
                Rollback();
                throw;
            }
        }

        /// <summary>Compact the database file (used after large imports/deletions).</summary>
        public void Vacuum()
        {
            Execute("PRAGMA wal_checkpoint(TRUNCATE);");
            Execute("VACUUM;");
        }

        public void Checkpoint()
        {
            try { Execute("PRAGMA wal_checkpoint(FULL);"); }
            catch (SqliteException) { }
        }

        private void EnsureOpen()
        {
            if (_db == IntPtr.Zero) { throw new ObjectDisposedException("SqliteDatabase", "The container database is already closed."); }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_db == IntPtr.Zero) { return; }
                Sqlite3.Close(_db);
                _db = IntPtr.Zero;
            }
        }
    }

    /// <summary>A prepared statement.  Bind by 1 based index, then Step() through the rows.</summary>
    public sealed class SqliteStatement : IDisposable
    {
        private readonly SqliteDatabase _owner;
        private IntPtr _stmt;
        private bool _done;
        private readonly string _sql;

        internal SqliteStatement(SqliteDatabase owner, IntPtr stmt, string sql)
        {
            _owner = owner;
            _stmt = stmt;
            _sql = sql;
        }

        public int ColumnCount { get { return Sqlite3.ColumnCount(_stmt); } }

        public string ColumnName(int index)
        {
            string name = Sqlite3.ColumnName(_stmt, index);
            if (string.IsNullOrEmpty(name)) { return "col" + index.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            return name;
        }

        // ------------------------------------------------------------- binding
        public void BindText(int index, string value)
        {
            if (value == null) { Sqlite3.BindNull(_stmt, index); return; }
            byte[] bytes = Utf8.Encoding.GetBytes(value);
            Sqlite3.BindText(_stmt, index, bytes, bytes.Length);
        }

        public void BindInt(int index, long value) { Sqlite3.BindInt64(_stmt, index, value); }
        public void BindDouble(int index, double value) { Sqlite3.BindDouble(_stmt, index, value); }
        public void BindNull(int index) { Sqlite3.BindNull(_stmt, index); }

        public void BindBlob(int index, byte[] value)
        {
            if (value == null) { Sqlite3.BindNull(_stmt, index); return; }
            Sqlite3.BindBlob(_stmt, index, value, value.Length);
        }

        /// <summary>Bind a positional parameter list; null → NULL, long/double/bool/string/byte[] supported.</summary>
        public void BindAll(object[] parameters)
        {
            if (parameters == null) { return; }
            for (int i = 0; i < parameters.Length; i++)
            {
                object value = parameters[i];
                int index = i + 1;
                if (value == null) { BindNull(index); continue; }
                if (value is string) { BindText(index, (string)value); continue; }
                if (value is bool) { BindInt(index, ((bool)value) ? 1 : 0); continue; }
                if (value is int) { BindInt(index, (int)value); continue; }
                if (value is long) { BindInt(index, (long)value); continue; }
                if (value is double) { BindDouble(index, (double)value); continue; }
                if (value is float) { BindDouble(index, (float)value); continue; }
                if (value is decimal) { BindDouble(index, (double)(decimal)value); continue; }
                if (value is DateTime) { BindText(index, IsoTime.Format((DateTime)value)); continue; }
                if (value is byte[]) { BindBlob(index, (byte[])value); continue; }
                BindText(index, value.ToString());
            }
        }

        // ------------------------------------------------------------- reading
        public bool Step()
        {
            if (_done) { return false; }
            int rc = Sqlite3.Step(_stmt);
            if (rc == Sqlite3.Row) { return true; }
            _done = true;
            if (rc == Sqlite3.Done) { return false; }
            throw new SqliteException("SQL step failed: " + Sqlite3.ErrorMessage(_owner.Handle) + " [" + _sql + "]", rc);
        }

        public bool IsNull(int column)
        {
            return Sqlite3.ColumnType(_stmt, column) == Sqlite3.Null;
        }

        public string GetString(int column)
        {
            if (IsNull(column)) { return null; }
            int bytes = Sqlite3.ColumnBytes(_stmt, column);
            return Utf8.Read(Sqlite3.ColumnText(_stmt, column), bytes);
        }

        public long GetInt64(int column)
        {
            if (IsNull(column)) { return 0; }
            return Sqlite3.ColumnInt64(_stmt, column);
        }

        public int GetInt(int column)
        {
            return (int)GetInt64(column);
        }

        public double GetDouble(int column)
        {
            if (IsNull(column)) { return 0; }
            int type = Sqlite3.ColumnType(_stmt, column);
            if (type == Sqlite3.Integer) { return Sqlite3.ColumnInt64(_stmt, column); }
            return Sqlite3.ColumnDouble(_stmt, column);
        }

        public bool GetBool(int column)
        {
            return GetInt64(column) != 0;
        }

        public byte[] GetBlob(int column)
        {
            if (IsNull(column)) { return null; }
            IntPtr pointer = Sqlite3.ColumnBlob(_stmt, column);
            int length = Sqlite3.ColumnBytes(_stmt, column);
            if (pointer == IntPtr.Zero || length <= 0) { return new byte[0]; }
            byte[] buffer = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(pointer, buffer, 0, length);
            return buffer;
        }

        public object GetValue(int column)
        {
            switch (Sqlite3.ColumnType(_stmt, column))
            {
                case Sqlite3.Null: return null;
                case Sqlite3.Integer: return Sqlite3.ColumnInt64(_stmt, column);
                case Sqlite3.Float: return Sqlite3.ColumnDouble(_stmt, column);
                case Sqlite3.Blob: return GetBlob(column);
                default: return GetString(column);
            }
        }

        /// <summary>Run an INSERT/UPDATE/DELETE once and return the number of changed rows.</summary>
        public int RunToCompletion()
        {
            while (Step()) { }
            return _owner.Changes();
        }

        /// <summary>Reset the statement so it can be executed again with fresh bindings.</summary>
        public void Reset()
        {
            Sqlite3.Reset(_stmt);
            Sqlite3.ClearBindings(_stmt);
            _done = false;
        }

        public void Dispose()
        {
            if (_stmt == IntPtr.Zero) { return; }
            Sqlite3.Finalize(_stmt);
            _stmt = IntPtr.Zero;
        }
    }
}
