//-----------------------------------------------------------------------------
// FILE:        SqlContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Merges the .NET concepts of a SqlConnection and SqlTransaction.

#if DEBUG
#undef TRACK        // Define this to enable code to help figure out where
                    // orphaned contexts came from.
#endif

using System;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

using LillTek.Common;

namespace LillTek.Data
{
    /// <summary>
    /// Merges the .NET concepts of <see cref="SqlConnection" /> and <see cref="SqlTransaction" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to integrate some of the functionality of
    /// a <see cref="SqlConnection" /> with a <see cref="SqlTransaction" />.  Think of a 
    /// <see cref="SqlContext" /> as a replacement for a <see cref="SqlConnection" />.
    /// </para>
    /// <para>
    /// Instantiate a <see cref="SqlContext" /> with a database connection string and
    /// then call <see cref="Open" /> to establish a connection with the server. 
    /// SqlCommand objects are created by calling CreateCommand() and
    /// can be executed normally.  Note that care must be taken to ensure
    /// that <see cref="Close" /> is called promptly when the context is no longer
    /// needed or when an exception is thrown to ensure that the underlying
    /// database connection will be promptly returned to the connection
    /// pool.
    /// </para>
    /// <para>
    /// Transaction support is integrated into the SqlContext implementation.
    /// Simply call <see cref="BeginTransaction" /> to start a transaction and 
    /// <see cref="Commit" /> or <see cref="Rollback" /> to complete it.  
    /// Transactions may be nested with this implementation.  This is implemented 
    /// under the covers via SQL save points.  The SqlContext class automatically 
    /// handles the release of the transaction resources when a transaction is completed
    /// or the context is closed.
    /// </para>
    /// <para>
    /// The class also builds in support for the StdSuccess/StdError pattern
    /// used for returning error codes and messages from stored procedures.
    /// In this pattern, a two column single row result set will be returned
    /// before any informational result sets.  The first column of this
    /// row will hold an integer value.  This will be 0 if the operation
    /// completed successfully, non-zero otherwise.  The second column will
    /// contain a text error message.
    /// </para>
    /// <para>
    /// To use this functionality, pass a StdErrorCreateDelegate delegate
    /// instance to the constructor and then call <see cref="ExecuteReader(System.Data.SqlClient.SqlCommand)" /> 
    /// after executing a SQL command.  This method will read the stderror result
    /// set.  If the error code is 0, it will advance to the first data result set
    /// before returning.  If the error code is non-zero, the specified
    /// StdErrorCreateDelegate will be called which must construct an exception
    /// from the parameters passed and return it.  <see cref="ExecuteReader(System.Data.SqlClient.SqlCommand)" /> 
    /// will then throw this exception.
    /// </para>
    /// <para>
    /// Note that SqlContext instances will pass an error code or <b>-1</b> to
    /// the StdErrorCreateDelegate when it was unable to process the
    /// standard error result.
    /// </para>
    /// <para>
    /// For some strange reason, on long lived SqlContexts I occasionally 
    /// see SqlException(Number=11,Message="General network error") exceptions.
    /// I did some research on Google and found a few instances where
    /// other people have seen the same thing.  I'm going to handle this
    /// situation by retrying to reconnect to the server and execute the
    /// command again.
    /// </para>
    /// </remarks>
    public sealed class SqlContext : ITransactionContext
    {
        //---------------------------------------------------------------------
        // Static members

        private const string AlreadyOpenMsg  = "Context already open.";
        private const string NotOpenMsg      = "Context not open.";
        private const string NoTransMsg      = "No transaction pending.";
        private const string BadStdErrorMsg  = "Invalid StdError response from database.";
        private const string BadCreateMsg    = "Invalid object creation response from database.";
        private const string SavePointPrefix = "__sqlcontext_";
#if TRACK
        private static bool             traceWarning = false;
#endif

        //---------------------------------------------------------------------
        // Instance members

        private string                  conString;              // The connection string
        private SqlConnection           sqlCon;                 // The SQL connection (if open)
        private SqlTransaction          sqlTrans;               // The SQL transaction (or null)
        private int                     nextSavePoint;          // Next savepoint ID
        private Stack                   transactions;           // Stack of nested transaction savepoint names
        private StdErrorCreateDelegate  onStdError;             // The StdError exception factory (or null)
        private bool                    enableRetry = false;    // Enables reconnection attempts after
                                                                // losing a connection to the SQL server
        private bool                    retrying;               // True if retrying a SQL command
        private bool?                   isSqlAzure;             // True if connected to a SQL Azure database

#if TRACK
        // Used to implement orphaned context tracking

        private string                  lastCommand;            // Text of the last command created from this context
        private CallStack               createdAt;              // The stack at the time the context was created
#endif

        /// <summary>
        /// Initializes the context with the connection string passed.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <remarks>
        /// See the .NET Framework documentation for information on the format
        /// of this parameter.
        /// </remarks>
        public SqlContext(string connectionString)
        {
            this.conString     = connectionString;
            this.sqlCon        = null;
            this.sqlTrans      = null;
            this.transactions  = new Stack();
            this.nextSavePoint = 0;
            this.onStdError    = null;
            this.retrying      = false;
            this.isSqlAzure    = null;
#if TRACK
            this.lastCommand   = string.Empty;
            this.createdAt     = new CallStack(1,true);

            if (!traceWarning) 
            {
                SysLog.LogWarning("LillTek.Data.SqlContext TRACE enabled.");
                traceWarning = true;
            }
#endif
        }

        /// <summary>
        /// Release any resources.
        /// </summary>
        ~SqlContext()
        {
#if TRACK
            if (sqlCon != null) 
            {   
                var sb = new StringBuilder();

                sb.Append("SqlContext not closed properly.\r\n\r\n");
                sb.AppendFormat("last command [{0}].\r\n\r\n",lastCommand);
                sb.Append("Stack at creation:\r\n\r\n");
                sb.Append(createdAt.ToString());

                SysLog.LogError(sb.ToString());
            }
#endif
        }

        /// <summary>
        /// Initializes the context with the connection string passed
        /// and StdErrorCreateDelegate instance passed.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="onStdError">The StdError exception factory.</param>
        /// <remarks>
        /// See the .NET Framework documentation for information on the format
        /// of this parameter.
        /// </remarks>
        public SqlContext(string connectionString, StdErrorCreateDelegate onStdError)
            : this(connectionString)
        {
            this.onStdError = onStdError;
        }

        /// <summary>
        /// Opens a connection to the database.
        /// </summary>
        public void Open()
        {
            if (sqlCon != null)
                throw new InvalidOperationException(AlreadyOpenMsg);

            sqlCon = new SqlConnection(conString);
            try
            {
                sqlCon.Open();
            }
            catch
            {
                sqlCon = null;
                throw;
            }
        }

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If there are any transactions pending on this context, the
        /// method will roll them back and then throw an InvalidOperationException.
        /// </para>
        /// <note>
        /// It is not an error to call this on a closed context.
        /// </note>
        /// </remarks>
        public void Close()
        {
            try
            {
                if (sqlCon == null)
                    return;

                if (sqlTrans != null)
                    sqlTrans.Rollback();

                sqlCon.Close();

                if (sqlTrans != null)
                    throw new InvalidOperationException(string.Format("[{0}] nested transactions pending.", transactions.Count));
            }
            finally
            {
                sqlCon   = null;
                sqlTrans = null;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources associated with the context.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Returns <c>true</c> if the context is connected to a SQL Azure database.
        /// </summary>
        public bool IsSqlAzure
        {
            get
            {
                if (sqlCon == null)
                    throw new InvalidOperationException(NotOpenMsg);

                if (!isSqlAzure.HasValue)
                {
                    var ds = ExecuteSet(CreateCommand("select serverproperty('edition')"));
                    var dt = ds.Tables[0];

                    if (dt.Rows.Count != 1)
                        isSqlAzure = false;
                    else
                        isSqlAzure = String.Compare(SqlHelper.AsString(dt.Rows[0][0]), "SQL Azure", true) == 0;
                }

                return isSqlAzure.Value;
            }
        }

        /// <summary>
        /// Creates a SQL command bound to this context.
        /// </summary>
        /// <param name="cmdText">The command text with optional formatting arguments.</param>
        /// <param name="args">Optional arguments.</param>
        /// <returns>A SqlCommand instance.</returns>
        public SqlCommand CreateCommand(string cmdText, params object[] args)
        {
            if (sqlCon == null)
                throw new InvalidOperationException(NotOpenMsg);

            if (args.Length > 0)
                cmdText = string.Format(cmdText, args);
#if TRACK
            lastCommand = cmdText;
#endif
            return new SqlCommand(cmdText, sqlCon, sqlTrans);
        }

        /// <summary>
        /// Creates a SQL stored procedure command bound to this context.
        /// </summary>
        /// <param name="procName">The SQL stored procedure name.</param>
        /// <returns>A SqlCommand instance.</returns>
        public SqlCommand CreateSPCall(string procName)
        {
            SqlCommand sqlCmd;

            if (sqlCon == null)
                throw new InvalidOperationException(NotOpenMsg);
#if TRACK
            lastCommand = "exec " + procName;
#endif
            sqlCmd = new SqlCommand(procName, sqlCon, sqlTrans);
            sqlCmd.CommandType = CommandType.StoredProcedure;

            return sqlCmd;
        }

        /// <summary>
        /// Determines whether the current state of the context and the 
        /// exception thrown during a transaction indicates that we should
        /// attempt to retry the connection on a new SqlConnection.
        /// </summary>
        /// <param name="sqlErr">The SqlException thrown.</param>
        /// <returns><c>true</c> if the transaction should be retried.</returns>
        /// <remarks>
        /// <para>
        /// This method will return <c>true</c> if the exception indicates
        /// a general network error, the context is not within a nested
        /// transaction, and we haven't already retried the connection.
        /// </para>
        /// <note>
        /// This method will also set the local "retrying" member to
        /// the value returned.
        /// </note>
        /// </remarks>
        private bool Reconnect(SqlException sqlErr)
        {
            if (retrying || !enableRetry)
                return false;

            retrying = sqlErr.Number == 11 && transactions.Count == 0;
            if (!retrying)
                return false;

            try
            {
                sqlCon.Close();
                sqlCon = new SqlConnection(conString);
                sqlCon.Open();
            }
            catch
            {
                sqlCon = null;
                return false;
            }

            retrying = true;
            return true;
        }

        /// <summary>
        /// Calls the database's <b>GetProductInfo</b> stored procedure to verify
        /// that the database's product ID and schema version match the parameters
        /// passed.
        /// </summary>
        /// <param name="productID">The expected product ID string.</param>
        /// <param name="versionString">The expected schema version (encoded as a string).</param>
        /// <exception cref="SqlSchemaMismatchException">Thrown if the stored procedure call failed or if the arguments don't match the values in the database.</exception>
        /// <remarks>
        /// <para>
        /// This method compares the <paramref name="versionString" /> passed against the version
        /// stored in the database and succeeds if the version requested is greater than or equal
        /// to the current database version.
        /// </para>
        /// <note>
        /// This method compares only the <see cref="Version.Major "/>, <see cref="Version.Minor" />,
        /// and <see cref="Version.Build" /> properties and ignores <see cref="Version.Revision" />.
        /// The idea here is that changes to <see cref="Version.Revision" /> would be used to identify
        /// database updates that are not structural or will not impact existing applications.
        /// </note>
        /// </remarks>
        public void VerifySchema(string productID, string versionString)
        {
            string      dbProductID;
            string      dbVersionString;
            Version     version;
            Version     dbVersion;

            if (sqlCon == null)
                throw new InvalidOperationException(NotOpenMsg);

            try
            {
                var dt = ExecuteTable(CreateSPCall("GetProductInfo"));

                if (dt.Rows.Count == 0)
                    throw new SqlSchemaMismatchException("[GetProductInfo] stored procedure didn't return anything.");

                dbProductID = SqlHelper.AsString(dt.Rows[0]["ProductID"]);
                dbVersionString = SqlHelper.AsString(dt.Rows[0]["SchemaVersion"]);
            }
            catch (Exception e)
            {
                throw new SqlSchemaMismatchException("Error executing [GetProductInfo]: {0}", e.Message);
            }

            if (productID != dbProductID)
                throw new SqlSchemaMismatchException("Database product ID [{0}] does not match the expected [{1}].", dbProductID, productID);

            version   = new Version(versionString);
            dbVersion = new Version(dbVersionString);

            if (new Version(version.Major, version.Minor, version.Build) > new Version(dbVersion.Major, dbVersion.Minor, dbVersion.Build))
                throw new SqlSchemaMismatchException("Database schema version [{0}] does not match the expected [{0}].", dbVersion, version);
        }

        /// <summary>
        /// Initiates a database transaction.
        /// </summary>
        /// <param name="iso">The isolation level to use.</param>
        /// <remarks>
        /// <para>
        /// Database transactions may be nested.  This class implements this
        /// via save points.  Note that every call to <see cref="BeginTransaction" />
        /// must be matched with a call to <see cref="Commit" /> or <see cref="Rollback" />.
        /// </para>
        /// <note>
        /// The isolation level will be ignored for nested
        /// transactions.
        /// </note>
        /// </remarks>
        public void BeginTransaction(IsolationLevel iso)
        {
            if (sqlCon == null)
                throw new InvalidOperationException(NotOpenMsg);

            if (sqlTrans == null)
            {
                sqlTrans = sqlCon.BeginTransaction(iso);
                transactions.Push(null);
            }
            else
            {
                var savePoint = SavePointPrefix + (nextSavePoint++).ToString();

                sqlTrans.Save(savePoint);
                transactions.Push(savePoint);
            }
        }

        /// <summary>
        /// Rolls back the current transaction.
        /// </summary>
        public void Rollback()
        {
            if (sqlCon == null)
                throw new InvalidOperationException(NotOpenMsg);

            if (sqlTrans == null)
                throw new InvalidCastException(NoTransMsg);

            try
            {
                if (transactions.Count == 1)
                {
                    // Rollback the real transaction.

                    transactions.Pop();
                    sqlTrans.Rollback();
                }
                else
                {
                    // Rollback to the last nested savepoint

                    var savePoint = (string)transactions.Pop();

                    sqlTrans.Rollback(savePoint);
                }
            }
            finally
            {
                if (transactions.Count == 0)
                {
                    sqlTrans.Dispose();
                    sqlTrans = null;
                }
            }
        }

        /// <summary>
        /// Commits the current transaction.
        /// </summary>
        public void Commit()
        {
            if (sqlCon == null)
                throw new InvalidOperationException(NotOpenMsg);

            if (sqlTrans == null)
                throw new InvalidCastException(NoTransMsg);

            try
            {
                if (transactions.Count == 1)
                {
                    // Commit the real transaction.

                    transactions.Pop();
                    sqlTrans.Commit();
                }
                else
                {
                    // This is a NOP except for popping the transaction save point name

                    transactions.Pop();
                }
            }
            finally
            {
                if (transactions.Count == 0)
                {
                    sqlTrans.Dispose();
                    sqlTrans = null;
                }
            }
        }

        /// <summary>
        /// Executes a textual SQL command and returns a <see cref="SqlDataReader" />.
        /// </summary>
        /// <param name="command">The command format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>A <see cref="SqlDataReader" /> referencing the first non-error result set.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convienent, be very careful using it
        /// to avoid SQL injection attacks.
        /// </note>
        /// </remarks>
        public SqlDataReader ExecuteReader(string command, params object[] args)
        {
            return ExecuteReader(CreateCommand(command, args));
        }

        /// <summary>
        /// Executes the command passed returning a <see cref="SqlDataReader" />.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <returns>A <see cref="SqlDataReader" />.</returns>
        public SqlDataReader ExecuteReader(SqlCommand sqlCmd)
        {
            retrying = false;

        retry: try
            {
                return ExecuteReader(sqlCmd, false);
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
        }

        /// <summary>
        /// Executes the command passed returning a <see cref="SqlDataReader" />, optionally 
        /// processing a StdError result set.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <param name="stdError">Pass as <c>true</c> to process StdError result sets.</param>
        /// <returns>A <see cref="SqlDataReader" /> referencing the first non-error result set.</returns>
        public SqlDataReader ExecuteReader(SqlCommand sqlCmd, bool stdError)
        {
            SqlDataReader   sqlReader;
            int             code;
            string          sproc;
            string          message;
            DataTable           schema;

            retrying = false;
        retry: try
            {
                sqlReader = null;

                if (!stdError)
                    return sqlCmd.ExecuteReader();

                if (sqlCon == null)
                    throw new InvalidOperationException(NotOpenMsg);

                try
                {
                    sqlReader = sqlCmd.ExecuteReader();
                    if (!sqlReader.Read())
                        throw new InvalidOperationException(BadStdErrorMsg);

                    schema = sqlReader.GetSchemaTable();
                    if (schema.Rows.Count != 3 ||
                        String.Compare((string)schema.Rows[0]["ColumnName"], "Code", true) != 0 ||
                        String.Compare((string)schema.Rows[1]["ColumnName"], "Procedure", true) != 0 ||
                        String.Compare((string)schema.Rows[2]["ColumnName"], "Message", true) != 0)
                    {
                        throw onStdError(-1, sqlCmd.CommandText, "Invalid StdError result set.");
                    }

                    code    = sqlReader.GetInt32(0);
                    sproc   = sqlReader[1].ToString();
                    message = sqlReader[2].ToString();

                    if (code != 0)
                    {
                        if (onStdError == null)
                            throw new SqlStdErrorException(code, sproc, message);
                        else
                            throw onStdError(code, sproc, message);
                    }

                    sqlReader.NextResult();
                    return sqlReader;
                }
                catch
                {
                    if (sqlReader != null)
                        sqlReader.Close();

                    throw;
                }
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
        }

        /// <summary>
        /// Executes a formatted SQL command.
        /// </summary>
        /// <param name="command">The command format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <remarks>
        /// <note>
        /// Although this method is convienent, be very careful using it
        /// to avoid SQL injection attacks.
        /// </note>
        /// </remarks>
        public void Execute(string command, params object[] args)
        {
            Execute(CreateCommand(command, args));
        }

        /// <summary>
        /// Executes the command passed.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        public void Execute(SqlCommand sqlCmd)
        {
            retrying = false;

        retry: try
            {
                Execute(sqlCmd, false);
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
        }

        /// <summary>
        /// Executes the command passed, optionally processing a StdError result set.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <param name="stdError">Pass as <c>true</c> to process StdError result sets.</param>
        public void Execute(SqlCommand sqlCmd, bool stdError)
        {
            retrying = false;

        retry: try
            {
                if (stdError)
                {
                    SqlDataReader sqlReader = null;

                    try
                    {
                        sqlReader = ExecuteReader(sqlCmd, stdError);
                    }
                    finally
                    {
                        if (sqlReader != null)
                            sqlReader.Close();
                    }
                }
                else
                {
                    sqlCmd.ExecuteScalar();
                }
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
        }

        /// <summary>
        /// Executes the formatted command passed that creates a new database object.
        /// </summary>
        /// <param name="command">The command format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The ID of the new database object.</returns>
        /// <remarks>
        /// <para>
        /// In this design pattern, the database stored procedure returns a single row,
        /// single column result set.  The value in this result set is the ID of the 
        /// newly created object as an 8, 16, 32, or 64 bit integer.
        /// </para>
        /// <note>
        /// Although this method is convienent, be very careful using it
        /// to avoid SQL injection attacks.
        /// </note>
        /// </remarks>
        public long ExecuteCreate(string command, params object[] args)
        {
            return ExecuteCreate(CreateCommand(command, args));
        }

        /// <summary>
        /// Executes the command passed that creates a new database object.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <returns>The ID of the new database object.</returns>
        /// <remarks>
        /// In this design pattern, the database stored procedure returns a single row,
        /// single column result set.  The value in this result set is the ID of the 
        /// newly created object as an 8, 16, 32, or 64 bit integer.
        /// </remarks>
        public long ExecuteCreate(SqlCommand sqlCmd)
        {
            retrying = false;

        retry: try
            {
                return ExecuteCreate(sqlCmd, false);
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
        }

        /// <summary>
        /// Executes the command passed that creates a new database object and an optional 
        /// StdError result set.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <param name="stdError">Pass as <c>true</c> to process StdError result sets.</param>
        /// <returns>The ID of the new database object.</returns>
        /// <remarks>
        /// In this design pattern, the database stored procedure returns a single row,
        /// single column result set (after the optional StdError result set).  The
        /// value in this result set is the ID of the newly created object as an
        /// 8, 16, 32, or 64 bit integer.
        /// </remarks>
        public long ExecuteCreate(SqlCommand sqlCmd, bool stdError)
        {
            SqlDataReader sqlReader;

            retrying = false;

        retry: sqlReader = null;
            try
            {
                sqlReader = ExecuteReader(sqlCmd, stdError);

                if (!sqlReader.Read())
                    throw new InvalidOperationException(BadCreateMsg);

                if (sqlReader.FieldCount != 1)
                    throw new InvalidOperationException(BadCreateMsg);

                return sqlReader.GetInt64(0);
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
            finally
            {
                if (sqlReader != null)
                    sqlReader.Close();
            }
        }

        /// <summary>
        /// Executes the formatted command passed that returns a single result set.
        /// </summary>
        /// <param name="command">The command format string.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The DataTable instance holding the result set.</returns>
        /// <remarks>
        /// <note>
        /// Although this method is convienent, be very careful using it
        /// to avoid SQL injection attacks.
        /// </note>
        /// </remarks>
        public DataTable ExecuteTable(string command, params object[] args)
        {
            return ExecuteTable(CreateCommand(command, args));
        }

        /// <summary>
        /// Executes the command passed that returns a single result set.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <returns>The DataTable instance holding the result set.</returns>
        public DataTable ExecuteTable(SqlCommand sqlCmd)
        {
            retrying = false;

        retry: try
            {
                return ExecuteTable(sqlCmd, false);
            }
            catch (SqlException e)
            {

                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
        }

        /// <summary>
        /// Executes the command passed that returns a single result set and an optional 
        /// StdError result set.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <param name="stdError">Pass as <c>true</c> to process StdError result sets.</param>
        /// <returns>The DataTable instance holding the result set.</returns>
        public DataTable ExecuteTable(SqlCommand sqlCmd, bool stdError)
        {
            SqlDataReader   sqlReader;
            DataTable       schema;
            DataTable       table;
            object[]        rowData;

            retrying = false;

        retry: sqlReader = null;
            try
            {
                sqlReader = ExecuteReader(sqlCmd, stdError);

                // Initialize the result table from the result set's schema

                schema = sqlReader.GetSchemaTable();
                table = new DataTable();
                for (int i = 0; i < schema.Rows.Count; i++)
                    table.Columns.Add((string)schema.Rows[i]["ColumnName"], (System.Type)schema.Rows[i]["DataType"]);

                // Load the data

                while (sqlReader.Read())
                {
                    rowData = new object[sqlReader.FieldCount];
                    sqlReader.GetValues(rowData);
                    table.Rows.Add(rowData);
                }

                return table;
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
            finally
            {
                if (sqlReader != null)
                    sqlReader.Close();
            }
        }

        /// <summary>
        /// Executes the command passed that returns one or more result sets.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <returns>The DataSet instance holding the result sets.</returns>
        /// <remarks>
        /// A table will be added to the data set returned for each result set
        /// returned by the database.  The first result set's table will be named "0",
        /// the second "1", and so on.
        /// </remarks>
        public DataSet ExecuteSet(SqlCommand sqlCmd)
        {
            retrying = false;

        retry: try
            {
                return ExecuteSet(sqlCmd, false);
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
        }

        /// <summary>
        /// Executes the command passed that returns one or more result sets and an optional 
        /// StdError result set.
        /// </summary>
        /// <param name="sqlCmd">The SQL command.</param>
        /// <param name="stdError">Pass as <c>true</c> to process StdError result sets.</param>
        /// <returns>The DataSet instance holding the result sets.</returns>
        /// <remarks>
        /// A table will be added to the data set returned for each result set
        /// returned by the database.  The first result set's table will be named "0",
        /// the second "1", and so on.
        /// </remarks>
        public DataSet ExecuteSet(SqlCommand sqlCmd, bool stdError)
        {
            SqlDataReader   sqlReader;
            DataSet         dataSet;
            DataTable       schema;
            DataTable       table;
            object[]        rowData;

            retrying = false;

        retry: sqlReader = null;
            try
            {
                sqlReader = ExecuteReader(sqlCmd, stdError);
                dataSet   = new DataSet();

                do
                {
                    // Initialize the result table from the result set's schema

                    schema = sqlReader.GetSchemaTable();
                    table  = new DataTable(dataSet.Tables.Count.ToString());

                    for (int i = 0; i < schema.Rows.Count; i++)
                        table.Columns.Add((string)schema.Rows[i]["ColumnName"], (System.Type)schema.Rows[i]["DataType"]);

                    while (sqlReader.Read())
                    {
                        rowData = new object[sqlReader.FieldCount];
                        sqlReader.GetValues(rowData);
                        table.Rows.Add(rowData);
                    }

                    dataSet.Tables.Add(table);

                } 
                while (sqlReader.NextResult());

                return dataSet;
            }
            catch (SqlException e)
            {
                if (Reconnect(e))
                    goto retry;
                else
                    throw;
            }
            finally
            {
                if (sqlReader != null)
                    sqlReader.Close();
            }
        }
    }
}
