using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Learnest.Core;

namespace Learnest.Data
{
    /// <summary>
    /// Minimal, dependency free SQLite binding.
    ///
    /// Why hand written P/Invoke instead of Microsoft.Data.Sqlite:
    ///   * Learnest must stay a small native Windows app with no NuGet payload, and must keep
    ///     building both with the .NET 8/9 SDK and with the in-box .NET Framework compiler.
    ///   * SQLite already ships with Windows as winsqlite3.dll (10 1607 and later), so the app
    ///     can use a real, ACID, single file database with zero deployment cost.  When a user or
    ///     packaging step drops a portable sqlite3.dll next to the executable, that copy is used
    ///     instead so the container database version is fully under the app's control.
    ///
    /// Only the v2 statement API is used (prepare_v2/step/column_*/bind_*), which is present in
    /// both libraries.
    /// </summary>
    internal static class Sqlite3
    {
        internal const string AppLocalLibrary = "sqlite3.dll";
        internal const string SystemLibrary = "winsqlite3.dll";

        // Result codes
        internal const int Ok = 0;
        internal const int Row = 100;
        internal const int Done = 101;

        // Column types
        internal const int Integer = 1;
        internal const int Float = 2;
        internal const int Text = 3;
        internal const int Blob = 4;
        internal const int Null = 5;

        // Bind destructor sentinel: SQLITE_TRANSIENT copies the data immediately.
        internal static readonly IntPtr Transient = new IntPtr(-1);

        private static string _libraryInUse = "";
        private static string _version = "";

        /// <summary>Native library that satisfied the first successful call.</summary>
        internal static string LibraryInUse { get { return _libraryInUse; } }
        internal static string Version { get { return _version; } }

        internal sealed class AppLocal
        {
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_libversion")]
            internal static extern IntPtr libversion();
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_open")]
            internal static extern int open(IntPtr filename, out IntPtr db);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close_v2")]
            internal static extern int close_v2(IntPtr db);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close")]
            internal static extern int close(IntPtr db);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_exec")]
            internal static extern int exec(IntPtr db, IntPtr sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_prepare_v2")]
            internal static extern int prepare_v2(IntPtr db, IntPtr sql, int bytes, out IntPtr stmt, out IntPtr tail);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_step")]
            internal static extern int step(IntPtr stmt);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
            internal static extern int finalize(IntPtr stmt);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_reset")]
            internal static extern int reset(IntPtr stmt);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_clear_bindings")]
            internal static extern int clear_bindings(IntPtr stmt);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_text")]
            internal static extern int bind_text(IntPtr stmt, int index, byte[] value, int bytes, IntPtr destructor);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_int64")]
            internal static extern int bind_int64(IntPtr stmt, int index, long value);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_double")]
            internal static extern int bind_double(IntPtr stmt, int index, double value);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_null")]
            internal static extern int bind_null(IntPtr stmt, int index);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_blob")]
            internal static extern int bind_blob(IntPtr stmt, int index, byte[] value, int bytes, IntPtr destructor);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_text")]
            internal static extern IntPtr column_text(IntPtr stmt, int column);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_int64")]
            internal static extern long column_int64(IntPtr stmt, int column);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_double")]
            internal static extern double column_double(IntPtr stmt, int column);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_blob")]
            internal static extern IntPtr column_blob(IntPtr stmt, int column);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_bytes")]
            internal static extern int column_bytes(IntPtr stmt, int column);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_type")]
            internal static extern int column_type(IntPtr stmt, int column);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_count")]
            internal static extern int column_count(IntPtr stmt);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_errmsg")]
            internal static extern IntPtr errmsg(IntPtr db);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_changes")]
            internal static extern int changes(IntPtr db);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_last_insert_rowid")]
            internal static extern long last_insert_rowid(IntPtr db);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_extended_errcode")]
            internal static extern int extended_errcode(IntPtr db);
            [DllImport(AppLocalLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_name")]
            internal static extern IntPtr column_name(IntPtr stmt, int column);
        }

        internal sealed class SystemLocal
        {
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_libversion")]
            internal static extern IntPtr libversion();
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_open")]
            internal static extern int open(IntPtr filename, out IntPtr db);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close_v2")]
            internal static extern int close_v2(IntPtr db);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close")]
            internal static extern int close(IntPtr db);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_exec")]
            internal static extern int exec(IntPtr db, IntPtr sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_prepare_v2")]
            internal static extern int prepare_v2(IntPtr db, IntPtr sql, int bytes, out IntPtr stmt, out IntPtr tail);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_step")]
            internal static extern int step(IntPtr stmt);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
            internal static extern int finalize(IntPtr stmt);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_reset")]
            internal static extern int reset(IntPtr stmt);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_clear_bindings")]
            internal static extern int clear_bindings(IntPtr stmt);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_text")]
            internal static extern int bind_text(IntPtr stmt, int index, byte[] value, int bytes, IntPtr destructor);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_int64")]
            internal static extern int bind_int64(IntPtr stmt, int index, long value);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_double")]
            internal static extern int bind_double(IntPtr stmt, int index, double value);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_null")]
            internal static extern int bind_null(IntPtr stmt, int index);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_bind_blob")]
            internal static extern int bind_blob(IntPtr stmt, int index, byte[] value, int bytes, IntPtr destructor);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_text")]
            internal static extern IntPtr column_text(IntPtr stmt, int column);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_int64")]
            internal static extern long column_int64(IntPtr stmt, int column);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_double")]
            internal static extern double column_double(IntPtr stmt, int column);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_blob")]
            internal static extern IntPtr column_blob(IntPtr stmt, int column);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_bytes")]
            internal static extern int column_bytes(IntPtr stmt, int column);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_type")]
            internal static extern int column_type(IntPtr stmt, int column);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_count")]
            internal static extern int column_count(IntPtr stmt);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_errmsg")]
            internal static extern IntPtr errmsg(IntPtr db);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_changes")]
            internal static extern int changes(IntPtr db);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_last_insert_rowid")]
            internal static extern long last_insert_rowid(IntPtr db);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_extended_errcode")]
            internal static extern int extended_errcode(IntPtr db);
            [DllImport(SystemLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_name")]
            internal static extern IntPtr column_name(IntPtr stmt, int column);
        }

        /// <summary>Whether a portable sqlite3.dll sits next to the executable.</summary>
        internal static bool AppLocalAvailable()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return File.Exists(Path.Combine(baseDir, "sqlite3.dll"));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Probe which native library can actually be loaded, preferring an app local
        /// portable sqlite3.dll.  Safe to call repeatedly.
        /// </summary>
        internal static bool Probe(out string error)
        {
            error = null;
            if (AppLocalAvailable())
            {
                try
                {
                    _version = Utf8.Read(AppLocal.libversion());
                    _libraryInUse = AppLocalLibrary;
                    return true;
                }
                catch (Exception ex)
                {
                    error = "App-local sqlite3.dll could not be loaded: " + ex.Message;
                }
            }
            try
            {
                _version = Utf8.Read(SystemLocal.libversion());
                _libraryInUse = SystemLibrary;
                return true;
            }
            catch (Exception ex)
            {
                error = (error == null ? "" : error + " / ") + "winsqlite3.dll could not be loaded: " + ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------------ dispatch
        private static bool UseAppLocal()
        {
            if (string.IsNullOrEmpty(_libraryInUse)) { string ignored; Probe(out ignored); }
            return _libraryInUse == AppLocalLibrary;
        }

        internal static int Open(IntPtr filename, out IntPtr db)
        {
            return UseAppLocal() ? AppLocal.open(filename, out db) : SystemLocal.open(filename, out db);
        }

        internal static int Close(IntPtr db)
        {
            return UseAppLocal() ? AppLocal.close_v2(db) : SystemLocal.close_v2(db);
        }

        internal static int Exec(IntPtr db, IntPtr sql, out IntPtr errmsg)
        {
            return UseAppLocal()
                ? AppLocal.exec(db, sql, IntPtr.Zero, IntPtr.Zero, out errmsg)
                : SystemLocal.exec(db, sql, IntPtr.Zero, IntPtr.Zero, out errmsg);
        }

        internal static int Prepare(IntPtr db, IntPtr sql, out IntPtr stmt)
        {
            IntPtr tail;
            return UseAppLocal()
                ? AppLocal.prepare_v2(db, sql, -1, out stmt, out tail)
                : SystemLocal.prepare_v2(db, sql, -1, out stmt, out tail);
        }

        internal static int Step(IntPtr stmt)
        {
            return UseAppLocal() ? AppLocal.step(stmt) : SystemLocal.step(stmt);
        }

        internal static int Finalize(IntPtr stmt)
        {
            return UseAppLocal() ? AppLocal.finalize(stmt) : SystemLocal.finalize(stmt);
        }

        internal static int Reset(IntPtr stmt)
        {
            return UseAppLocal() ? AppLocal.reset(stmt) : SystemLocal.reset(stmt);
        }

        internal static int ClearBindings(IntPtr stmt)
        {
            return UseAppLocal() ? AppLocal.clear_bindings(stmt) : SystemLocal.clear_bindings(stmt);
        }

        internal static int BindText(IntPtr stmt, int index, byte[] value, int bytes)
        {
            return UseAppLocal()
                ? AppLocal.bind_text(stmt, index, value, bytes, Transient)
                : SystemLocal.bind_text(stmt, index, value, bytes, Transient);
        }

        internal static int BindInt64(IntPtr stmt, int index, long value)
        {
            return UseAppLocal() ? AppLocal.bind_int64(stmt, index, value) : SystemLocal.bind_int64(stmt, index, value);
        }

        internal static int BindDouble(IntPtr stmt, int index, double value)
        {
            return UseAppLocal() ? AppLocal.bind_double(stmt, index, value) : SystemLocal.bind_double(stmt, index, value);
        }

        internal static int BindNull(IntPtr stmt, int index)
        {
            return UseAppLocal() ? AppLocal.bind_null(stmt, index) : SystemLocal.bind_null(stmt, index);
        }

        internal static int BindBlob(IntPtr stmt, int index, byte[] value, int bytes)
        {
            return UseAppLocal()
                ? AppLocal.bind_blob(stmt, index, value, bytes, Transient)
                : SystemLocal.bind_blob(stmt, index, value, bytes, Transient);
        }

        internal static IntPtr ColumnText(IntPtr stmt, int column)
        {
            return UseAppLocal() ? AppLocal.column_text(stmt, column) : SystemLocal.column_text(stmt, column);
        }

        internal static long ColumnInt64(IntPtr stmt, int column)
        {
            return UseAppLocal() ? AppLocal.column_int64(stmt, column) : SystemLocal.column_int64(stmt, column);
        }

        internal static double ColumnDouble(IntPtr stmt, int column)
        {
            return UseAppLocal() ? AppLocal.column_double(stmt, column) : SystemLocal.column_double(stmt, column);
        }

        internal static IntPtr ColumnBlob(IntPtr stmt, int column)
        {
            return UseAppLocal() ? AppLocal.column_blob(stmt, column) : SystemLocal.column_blob(stmt, column);
        }

        internal static int ColumnBytes(IntPtr stmt, int column)
        {
            return UseAppLocal() ? AppLocal.column_bytes(stmt, column) : SystemLocal.column_bytes(stmt, column);
        }

        internal static int ColumnType(IntPtr stmt, int column)
        {
            return UseAppLocal() ? AppLocal.column_type(stmt, column) : SystemLocal.column_type(stmt, column);
        }

        internal static int ColumnCount(IntPtr stmt)
        {
            return UseAppLocal() ? AppLocal.column_count(stmt) : SystemLocal.column_count(stmt);
        }

        internal static string ErrorMessage(IntPtr db)
        {
            IntPtr message = UseAppLocal() ? AppLocal.errmsg(db) : SystemLocal.errmsg(db);
            return Utf8.Read(message);
        }

        internal static int Changes(IntPtr db)
        {
            return UseAppLocal() ? AppLocal.changes(db) : SystemLocal.changes(db);
        }

        internal static int ExtendedErrorCode(IntPtr db)
        {
            return UseAppLocal() ? AppLocal.extended_errcode(db) : SystemLocal.extended_errcode(db);
        }

        internal static string ColumnName(IntPtr stmt, int column)
        {
            return Utf8.Read(UseAppLocal() ? AppLocal.column_name(stmt, column) : SystemLocal.column_name(stmt, column));
        }

        internal static long LastInsertRowId(IntPtr db)
        {
            return UseAppLocal() ? AppLocal.last_insert_rowid(db) : SystemLocal.last_insert_rowid(db);
        }
    }

    /// <summary>Thrown for any SQLite failure; carries the native error text.</summary>
    public sealed class SqliteException : Exception
    {
        public int Code { get; private set; }

        public SqliteException(string message, int code) : base(message)
        {
            Code = code;
        }
    }
}
