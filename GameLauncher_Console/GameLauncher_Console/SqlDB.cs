﻿using Logger;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;

namespace SqlDB
{
    /// <summary>
    /// SQL database core implementation as a singleton pattern.
    /// Handle connections, executions and transactions
    /// The acual data read/write queries for Platform or Game objects should be defined elsewhere
    /// 
    /// TODOs: 
    ///     On startup, check if the database exists, if not create it and create any tables/constraints
    ///     Once the DB connection is made, perform a schema version check and apply new schema if necessary
    /// </summary>
    public sealed class CSqlDB
    {
        private const string SQL_MAIN_DATA_SOURCE = "database.db";
        private static CSqlDB m_instance = new CSqlDB();

        public static CSqlDB Instance
        {
            get
            {
                return m_instance;
            }
        }

        /// <summary>
        /// Open a separate data source and return the connection
        /// </summary>
        /// <param name="dataSource">Path to the data source</param>
        /// <returns><c>SQLiteConnection</c> object or <c>null</c> if not found</returns>
        public static SQLiteConnection OpenSeparateConnection(string dataSource)
        {
            if(!File.Exists(dataSource))
            {
                return null;
            }
            SQLiteConnection connection = new SQLiteConnection("Data source=" + dataSource + ";Version=3;New=False;Compress=True;");
            try
            {
                CLogger.LogInfo("Connecting to Data Source " + dataSource);
                connection.Open();
            }
            catch (SQLiteException e)
            {
                CLogger.LogWarn("ERROR: Database connection could not be established: " + e.ResultCode);
                return null;
            }
            return connection;
        }

        static CSqlDB()
        {
            
        }

        /// <summary>
        /// Create the connection to the database
        /// </summary>
        private CSqlDB()
        {

        }

        /// <summary>
        /// Check if the database connection is open
        /// </summary>
        /// <returns><c>True</c> if open, otherwise <c>false</c></returns>
        public bool IsOpen()
        {
            return (m_sqlite_conn != null) && (m_sqlite_conn.State == System.Data.ConnectionState.Open);
        }

        /// <summary>
        /// Create SQLiteConnection and open the data source
        /// </summary>
        /// <param name="create">If <c>true</c> and data source is not found, create new one and apply schema</param>
        /// <param name="dataSource">Path to the data source</param>
        /// <returns>SQL success/failure error code</returns>
        public SQLiteErrorCode Open(bool create, string dataSource = SQL_MAIN_DATA_SOURCE)
        {
            if (IsOpen())
            {
                return SQLiteErrorCode.Ok;
            }

            bool exists = File.Exists(dataSource);
            if(exists || create) // Only open if it's there or if we're making a new one
            {
                m_sqlite_conn = new SQLiteConnection("Data source=" + dataSource + ";Version=3;New=False;Compress=True;");
                try
                {
                    CLogger.LogInfo("Connecting to Data Source " + dataSource);
                    m_sqlite_conn.Open();
                }
                catch (SQLiteException e)
                {
                    CLogger.LogWarn("ERROR: Database connection could not be established: " + e.ResultCode);
                    return e.ResultCode;
                }
            }

            if(create && !exists) // New DB. Apply schema
            {
                string script;
                try
                {
                    script = File.ReadAllText("CreateDB.sql");
                }
                catch (IOException e)
                {
                    CLogger.LogError(e);
                    return SQLiteErrorCode.Unknown; // Use unknown for non-sql issues
                }

                if (script.Length > 0)
                {
                    Execute(script);
                }
            }
            else
            {
                return SQLiteErrorCode.Error;
            }
            return MaybeUpdateSchema();
        }

        /// <summary>
        /// Check database schema and update if out of data
        /// </summary>
        /// <returns>SQL success/failure status code</returns>
        private SQLiteErrorCode MaybeUpdateSchema()
        {
            /* TODOs:
                    Find the latest 'SCHEMA_VERSION' attribute in SystemAttribute table,
                    Check if there exist any shecma files with higher version (each schema file should look like this: schema_x.y.z.sql
                    Apply any schema files in ascending order and update 'SCHEMA_VERSION' attribute
            */
            return SQLiteErrorCode.Ok;
        }

        /// <summary>
        /// Execure SQL query
        /// </summary>
        /// <param name="qry">The SQL query</param>
        /// <returns>SQL success/failure status code</returns>
        public SQLiteErrorCode Execute(string qry)
        {
            SQLiteCommand cmd;
            cmd = m_sqlite_conn.CreateCommand();
            cmd.CommandText = qry;
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException e)
            {
                CLogger.LogWarn("Could not execute statement\n" + qry);
                CLogger.LogWarn(e.Message);
                return e.ResultCode;
            }
            return SQLiteErrorCode.Ok;
        }

        /// <summary>
        /// Execure SQL select statement
        /// </summary>
        /// <param name="qry">The SQL query</param>
        /// <param name="dataReader">SQLiteDataReader which contains the SELECT result</param>
        /// <returns>SQL success/failure status code</returns>
        public SQLiteErrorCode ExecuteRead(string qry, out SQLiteDataReader dataReader)
        {
            SQLiteCommand cmd;
            cmd = m_sqlite_conn.CreateCommand();
            cmd.CommandText = qry;
            try
            {
                dataReader = cmd.ExecuteReader();
            }
            catch (SQLiteException e)
            {
                CLogger.LogWarn("Could not execute statement\n" + qry);
                CLogger.LogWarn(e.Message);
                dataReader = null;
                return e.ResultCode;
            }
            return SQLiteErrorCode.Ok;
        }
        private SQLiteConnection m_sqlite_conn;
    }

    /// <summary>
    /// Abstract base class for SQL fields
    /// SQL field represents a single table column which holds the column name and the value
    /// Any derived classes can be used to read and write data to the specified column
    /// </summary>
    public abstract class CSqlField
    {
        /// <summary>
        /// Flags to describe the field's part in the query
        /// Will be used during statement construction
        /// </summary>
        public enum QryFlag
        {
            cSelRead  = 0x01, // Read field
            cUpdWrite = 0x02, // Update field
            cInsWrite = 0x04, // Insert field
            cSelWhere = 0x10, // SELECT WHERE field
            cUpdWhere = 0x20, // UPDATE WHERE field
            cDelWhere = 0x40, // DELETE WHERE field

            cReadWrite  = 0x0f, // All read/write flags
            cWhere      = 0xf0, // All where flags
            cAll        = 0xff,
        }

        protected string m_value; // Column value

        protected CSqlField(string column, QryFlag flag)
        {
            Column  = column;
            Flag    = flag;
            MakeNull();
        }

        /// <summary>
        /// Set the value to null
        /// </summary>
        public void MakeNull()
        {
            m_value = null;
        }

        /// <summary>
        /// Check if the value is null
        /// </summary>
        /// <returns>True if null, otherwise false</returns>
        public bool IsNull()
        {
            return m_value == null;
        }

        // Properties
        public string Column { get; private set; }
        public QryFlag Flag  { get; private set; }
        public TypeCode Type { get; protected set; }

        // Column value getter and setter properties
        public string String 
        {
            get { return m_value;  }
            set { m_value = value; }
        }

        public int Integer
        {
            get
            {
                int i;
                Int32.TryParse(m_value, out i);
                return i;
            }
            set { m_value = value.ToString(); }
        }

        public double Double
        {
            get
            {
                double d;
                double.TryParse(m_value, out d);
                return d;
            }
            set { m_value = value.ToString(); }
        }

        public bool Bool
        {
            get { return m_value == "1"; }
            set { m_value = (value) ? "1" : "0"; }
        }
    }

    // CSqlField-derived classes
    public class CSqlFieldString : CSqlField
    {
        public CSqlFieldString(string columnName, QryFlag qryFlag) : base(columnName, qryFlag)
        {
            Type = TypeCode.String;
            String = "";
        }
    }
    public class CSqlFieldInteger : CSqlField
    {
        public CSqlFieldInteger(string columnName, QryFlag qryFlag) : base(columnName, qryFlag)
        {
            Type = TypeCode.Int32;
            Integer = 0;
        }
    }
    public class CSqlFieldDouble : CSqlField
    {
        public CSqlFieldDouble(string columnName, QryFlag qryFlag) : base(columnName, qryFlag)
        {
            Type = TypeCode.Double;
            Double = 0.0;
        }
    }
    public class CSqlFieldBoolean : CSqlField
    {
        public CSqlFieldBoolean(string columnName, QryFlag qryFlag) : base(columnName, qryFlag)
        {
            Type = TypeCode.Boolean;
            Bool = false;
        }
    }

    /// <summary>
    /// SQL row class, implementing the Dictionary collection
    /// Preserves the insert order of the row definitions for extended queries
    /// </summary>
    public class CSqlRow : Dictionary<string, CSqlField>
    {
        private List<string> m_insertOrder;
        public CSqlRow() : base()
        {
            m_insertOrder = new List<string>();
        }

        /// <summary>
        /// Integer indexer.
        /// Return CSqlField from the insert order index
        /// </summary>
        /// <param name="index">the insert order index</param>
        /// <returns>CSqlField</returns>
        public CSqlField this[int index]
        {
            get
            {
                return base[m_insertOrder[index]];
            }
        }

        /// <summary>
        /// String indexer overload
        /// Return CSqlField from the string key
        /// If inserting new key-value pair, add the key to the insert order list
        /// </summary>
        /// <param name="key">Field column name</param>
        /// <returns>CSqlField</returns>
        public CSqlField this[string key]
        {
            get { return base[key]; }
            set
            {
                if(!this.ContainsKey(key))
                {
                    m_insertOrder.Add(key);
                }
                base[key] = value;
            }
        }
    }

    /// <summary>
    /// Abstract base class for managing SQL queries
    /// This class contains all query information such as 
    /// 
    /// TODOs:
    ///     Some extra comments/documentation wouldn't hurt
    ///     Support fot he following:
    ///         <, <=, >=, >, != , IS NULL, IS NOT NULL, IN (multiple params)
    ///         ALL JOINS
    ///         ORDER BY, GROUP BY
    ///     As of now, this stuff is probably not super important and can be added when needed
    /// </summary>
    public abstract class CSqlQry
    {
        private const string M_FIELD_MASK = "&";
        private const string M_VALUE_MASK = "?";

        protected readonly string m_tableName;
        protected readonly string m_selectCondition;
        protected readonly string m_selectExtraCondition;

        protected CSqlRow m_sqlRow;
        protected SQLiteDataReader m_selectResult;

        /// <summary>
        /// Constructor, set up the table name and the select condition strings
        /// </summary>
        /// <param name="table">The table name. This string should also include any JOIN tables</param>
        /// <param name="selectCondition">Select condition string for more complex queries. Use the field and value masks as templates (eg. & <= ?), those will be populated when query is constructed based on the CSqlRow's field insert order</param>
        /// <param name="selectExtraCondition">Any additional select conditions such as ORDER BY or GROUP BY</param>
        protected CSqlQry(string table, string selectCondition, string selectExtraCondition)
        {
            m_tableName = table;
            m_selectCondition = selectCondition;
            m_selectExtraCondition = selectExtraCondition;
            m_selectResult = null;
            m_sqlRow = new CSqlRow();
        }

        public void MakeFieldsNull()
        {
            m_selectResult = null;
            foreach (KeyValuePair<string, CSqlField> field in m_sqlRow)
            {
                field.Value.MakeNull();
            }
        }

        /// <summary>
        /// Prepare the main statement body
        /// </summary>
        protected virtual string PrepareMainStatement(CSqlField.QryFlag stmtFlag)
        {
            string query = "";
            foreach (KeyValuePair<string, CSqlField> field in m_sqlRow)
            {
                if((field.Value.Flag & stmtFlag) == 0)
                {
                    continue;
                }
                if (query.Length > 0)
                {
                    query += ", ";
                }
                query += field.Key;
            }
            return query;
        }

        /// <summary>
        /// Prepare values for the INSERT statement
        /// </summary>
        protected virtual string PrepareInsertStatement()
        {
            string insertValues = "";
            foreach(KeyValuePair<string, CSqlField> field in m_sqlRow)
            {
                if ((field.Value.Flag & CSqlField.QryFlag.cInsWrite) == 0)
                {
                    continue;
                }
                if (insertValues.Length > 0)
                {
                    insertValues += ", ";
                }
                if(field.Value.Type == TypeCode.String)
                {
                    insertValues += "'";
                    insertValues += field.Value.String;
                    insertValues += "'";
                }
                else if(field.Value.Type == TypeCode.Double)
                {
                    insertValues += field.Value.Double;
                }
                else // Bool fields are 0/1
                {
                    insertValues += field.Value.Integer;
                }
            }
            return insertValues;
        }

        /// <summary>
        /// Prepare a WHERE condition string
        /// </summary>
        /// <returns>Constructed query condition. If pre-defined condition string exists, return advanced condition string</returns>
        protected virtual string PrepareWhereStatement(CSqlField.QryFlag whereFlag)
        {
            if(m_selectCondition.Length > 0)
            {
                return PrepareAdvancedWhereStatement();
            }
            string whereCondition = "";
            foreach (KeyValuePair<string, CSqlField> field in m_sqlRow)
            {
                if((field.Value.Flag & whereFlag) == 0 || field.Value.IsNull())
                {
                    continue;
                }
                if(whereCondition.Length > 0)
                {
                    whereCondition += " AND ";
                }
                whereCondition += field.Key;
                whereCondition += " = ";
                if(field.Value.Type == TypeCode.String)
                {
                    whereCondition += "'";
                    whereCondition += field.Value.String;
                    whereCondition += "'";
                }
                else if(field.Value.Type == TypeCode.Double)
                {
                    whereCondition += field.Value.Double;
                }
                else // Bool fields are 0/1
                {
                    whereCondition += field.Value.Integer;
                }
            }
            return whereCondition;
        }

        /// <summary>
        /// Construct advanced WHERE condition based on the predefined select string.
        /// </summary>
        /// <returns>Constructed query condition</returns>
        protected virtual string PrepareAdvancedWhereStatement()
        {
            int fieldIndex = 0; // Which SQL field we're on right now
            int charIndex  = 0; // Current character on the m_selectCondition
            StringBuilder whereCondition = new StringBuilder();

            while(charIndex > -1 && charIndex < m_selectCondition.Length)
            {
                // Value mask should always come after field mask
                charIndex = m_selectCondition.IndexOf(M_VALUE_MASK, charIndex);
                if(charIndex == -1 || charIndex >= m_selectCondition.Length)
                {
                    continue;
                }
                whereCondition.Replace(M_FIELD_MASK, m_sqlRow[fieldIndex].Column, charIndex, 1);

                if(m_sqlRow[fieldIndex].Type == TypeCode.String)
                {
                    string s = "'" + m_sqlRow[fieldIndex].String + "'";
                    whereCondition.Replace(M_VALUE_MASK, s, charIndex, 1);
                }
                else if(m_sqlRow[fieldIndex].Type == TypeCode.Double)
                {
                    whereCondition.Replace(M_VALUE_MASK, m_sqlRow[fieldIndex].Double.ToString(), charIndex, 1);
                }
                else
                {
                    whereCondition.Replace(M_VALUE_MASK, m_sqlRow[fieldIndex].Integer.ToString(), charIndex, 1);
                }
                fieldIndex++;
            }
            return whereCondition.ToString();
        }

        /// <summary>
        /// Prepare the main statement body for UPDATE queries
        /// </summary>
        /// <returns>query statement string</returns>
        protected virtual string PrepareUpdateStatement()
        {
            string update = "";
            foreach (KeyValuePair<string, CSqlField> field in m_sqlRow)
            {
                if ((field.Value.Flag & CSqlField.QryFlag.cUpdWrite) == 0)
                {
                    continue;
                }
                if (update.Length > 0)
                {
                    update += ", ";
                }
                update += field.Key;
                update += " = ";
                if (field.Value.Type == TypeCode.String)
                {
                    update += "'";
                    update += field.Value.String;
                    update += "'";
                }
                else if (field.Value.Type == TypeCode.Double)
                {
                    update += field.Value.Double;
                }
                else // Bool fields are 0/1
                {
                    update += field.Value.Integer;
                }
            }
            return update;
        }

        /// <summary>
        /// Prepare and execure SELECT statement
        /// </summary>
        /// <returns>SQL success/failure status code</returns>
        public SQLiteErrorCode Select()
        {
            m_selectResult = null;
            string mainStmt = PrepareMainStatement(CSqlField.QryFlag.cSelRead);
            string whereCondition = PrepareWhereStatement(CSqlField.QryFlag.cSelWhere);

            string query = "SELECT " + mainStmt + " FROM " + m_tableName;
            if(whereCondition.Length > 0)
            {
                query += " WHERE " + whereCondition;
            }
            if(m_selectExtraCondition.Length > 0)
            {
                query += " " + m_selectExtraCondition;
            }
            SQLiteErrorCode err = CSqlDB.Instance.ExecuteRead(query, out m_selectResult);
            if(err == SQLiteErrorCode.Ok)
            {
                if(!Fetch()) // Perform a fetch on the first row, if available
                {
                    return SQLiteErrorCode.NotFound;
                }
            }
            return err;
        }

        /// <summary>
        /// Prepare and execure INSERT statement
        /// </summary>
        /// <returns>SQL success/failure status code</returns>
        public SQLiteErrorCode Insert()
        {
            string mainStmt = PrepareMainStatement(CSqlField.QryFlag.cInsWrite);
            string insertValues = PrepareInsertStatement();

            string query = "INSERT INTO " + m_tableName + " ( " + mainStmt + ") ";
            if(insertValues.Length > 0)
            {
                query += " VALUES (" + insertValues + ") ";
            }
            return CSqlDB.Instance.Execute(query);
        }

        /// <summary>
        /// Prepare and execute UPDATE statement
        /// </summary>
        /// <returns>SQL success/failure status code</returns>
        public SQLiteErrorCode Update()
        {
            string mainStmt = PrepareUpdateStatement();
            string whereCondition = PrepareWhereStatement(CSqlField.QryFlag.cUpdWhere);

            string query = "UPDATE " + m_tableName + " SET " + mainStmt;
            if (whereCondition.Length > 0)
            {
                query += " WHERE " + whereCondition;
            }
            return CSqlDB.Instance.Execute(query);
        }

        /// <summary>
        /// Prepare and execure DELETE statement
        /// </summary>
        /// <returns>SQL success/failure status code</returns>
        public SQLiteErrorCode Delete()
        {
            m_selectResult = null;
            string whereCondition = PrepareWhereStatement(CSqlField.QryFlag.cDelWhere);

            string query = "DELETE FROM " + m_tableName;
            if(whereCondition.Length > 0)
            {
                query += " WHERE " + whereCondition;
            }
            return CSqlDB.Instance.Execute(query);
        }

        /// <summary>
        /// Fetch the next row from the select result
        /// </summary>
        /// <returns>False if reached end of results, otherwise true</returns>
        public bool Fetch()
        {
            if(m_selectResult == null)
            {
                return false;
            }
            if(!m_selectResult.Read()) // End of results.
            {
                m_selectResult = null;
                MakeFieldsNull();
                return false;
            }

            // Get the values
            foreach (KeyValuePair<string, CSqlField> field in m_sqlRow)
            {
                if((field.Value.Flag & CSqlField.QryFlag.cSelRead) > 0)
                {
                    field.Value.String = m_selectResult[field.Key].ToString();
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Class for handling Attribute tables
    /// Provide a table name without the attribute (ie. Game) and the 'Attribute' will be added in the construction
    /// MasterID (foreign key) must be specified before first use
    /// 
    /// TODOs:
    ///     When adding a new value, override the first index or increment the index and add
    ///     Support for more types
    /// </summary>
    public class CDbAttribute
    {
        /// <summary>
        /// Inner class providing a qury definition for the attribute tables
        /// </summary>
        protected class CQryAttribute : CSqlQry
        {
            private string m_columnFK;
            public CQryAttribute(string table) : base(table + "Attribute", "", "")
            {
                m_columnFK = table + "FK";
                m_sqlRow[m_columnFK]        = new CSqlFieldInteger(m_columnFK, CSqlField.QryFlag.cAll);
                m_sqlRow["AttributeName"]   = new CSqlFieldString("AttributeName"   , CSqlField.QryFlag.cAll);
                m_sqlRow["AttributeIndex"]  = new CSqlFieldInteger("AttributeIndex" , CSqlField.QryFlag.cAll);
                m_sqlRow["AttributeValue"]  = new CSqlFieldString("AttributeValue"  , CSqlField.QryFlag.cAll);
            }
            public int ForeignKey
            {
                get { return m_sqlRow[m_columnFK].Integer; }
                set { m_sqlRow[m_columnFK].Integer = value; }
            }
            public string AttributeName
            {
                get { return m_sqlRow["AttributeName"].String; }
                set { m_sqlRow["AttributeName"].String = value; }
            }
            public int AttributeIndex
            {
                get { return m_sqlRow["AttributeIndex"].Integer; }
                set { m_sqlRow["AttributeIndex"].Integer = value; }
            }
            public string AttributeValue
            {
                get { return m_sqlRow["AttributeValue"].String; }
                set { m_sqlRow["AttributeValue"].String = value; }
            }
        }
        private CQryAttribute m_qry;

        public int MasterID { get; set; }

        public CDbAttribute(string table)
        {
            m_qry = new CQryAttribute(table);
        }

        /// <summary>
        /// Retrieve a single attribute value
        /// </summary>
        /// <param name="attributeName">The attribute name</param>
        /// <param name="attributeIndex">The attribute index. Default = 0</param>
        /// <returns>AttributeVaue matching the query, or empty string if not found</returns>
        public string GetStringValue(string attributeName, int attributeIndex = 0)
        {
            m_qry.MakeFieldsNull();
            m_qry.ForeignKey        = MasterID;
            m_qry.AttributeName     = attributeName;
            m_qry.AttributeIndex    = attributeIndex;
            m_qry.Select();
            return (m_qry.AttributeValue == null) ? "" : m_qry.AttributeValue;
        }

        /// <summary>
        /// Retrieve an array of attribute values based on the attribute name
        /// </summary>
        /// <param name="attributeName">The attribute name</param>
        /// <returns>String array with attribute values (in index order), or empty array if nothing</returns>
        public string[] GetStringValues(string attributeName)
        {
            List<string> response = new List<string>();
            m_qry.MakeFieldsNull();
            m_qry.ForeignKey    = MasterID;
            m_qry.AttributeName = attributeName;
            if(m_qry.Select() != SQLiteErrorCode.Ok)
            {
                return response.ToArray(); // Empty array
            }

            do
            {
                response.Add((m_qry.AttributeValue == null) ? "" : m_qry.AttributeValue);
            } while(m_qry.Fetch());
            return response.ToArray();
        }

        /// <summary>
        /// Insert an attribute
        /// </summary>
        /// <param name="attributeName">Attribute name</param>
        /// <param name="attributeValue">Attribute value</param>
        /// <param name="attributeIndex">Attribute index. Default = 0</param>
        /// <returns>SQL success/fail status code</returns>
        public SQLiteErrorCode SetStringValue(string attributeName, string attributeValue, int attributeIndex = 0)
        {
            m_qry.MakeFieldsNull();
            m_qry.ForeignKey     = MasterID;
            m_qry.AttributeName  = attributeName;
            m_qry.AttributeIndex = attributeIndex;
            m_qry.AttributeValue = attributeValue;
            return m_qry.Insert();
        }
    }
}
