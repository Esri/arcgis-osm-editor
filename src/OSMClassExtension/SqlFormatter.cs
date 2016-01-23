// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System.Runtime.InteropServices;
using ESRI.ArcGIS.Geodatabase;

namespace ESRI.ArcGIS.OSM.OSMClassExtension
{
    /// <summary>Class for assisting in formatting SQL clauses</summary>
    /// <remarks>
    /// - Handles databases (i.e. Oracle 11g) where SQL identifiers are case-sensitive when delimiters are added.
    /// </remarks>
    [ComVisible(false)]
    public class SQLFormatter
    {
        private ITable _table = null;
        private bool _isCaseSensitive = false;
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;

        public SQLFormatter(ITable table)
        {
            _table = table;
            if (_table != null)
            {
                ISQLSyntax sqlSyntax = (ISQLSyntax)((IDataset)_table).Workspace;
                _isCaseSensitive = (sqlSyntax.GetDelimitedIdentifierCase() == true);
                _prefix = sqlSyntax.GetSpecialCharacter(esriSQLSpecialCharacters.esriSQL_DelimitedIdentifierPrefix);
                _suffix = sqlSyntax.GetSpecialCharacter(esriSQLSpecialCharacters.esriSQL_DelimitedIdentifierSuffix);
            }
        }

        ~SQLFormatter()
        {
            if (_table != null)
            {
                //ComReleaser.ReleaseCOMObject(_table);

                _table = null;
            }
        }

        /// <summary>Returns a valid, delimited SQL identifier for the given field name</summary>
        public string SqlIdentifier(string columnName)
        {
            if ((_table == null) || (string.IsNullOrEmpty(columnName)))
                return string.Empty;

            string identifier = columnName;

            if (_isCaseSensitive)
            {
                int idx = _table.Fields.FindField(columnName);
                if (idx >= 0)
                {
                    IField field = _table.Fields.get_Field(idx);
                    if (field != null)
                        identifier = field.Name;
                }
            }

            return _prefix + identifier + _suffix;
        }
    }


    /// <summary>Extension Methods for SQL Formatter</summary>
    [ComVisible(false)]
    public static class SqlFormatterExt
    {
        /// <summary>Get a valid, delimited SQL identifier for the given feature class field by field name</summary>
        public static string SqlIdentifier(this IFeatureClass fc, string columnName)
        {
            ITable table = fc as ITable;
            return SqlIdentifier(table, columnName);
        }

        /// <summary>Get a valid, delimited SQL identifier for the given table field by field name</summary>
        public static string SqlIdentifier(this ITable table, string columnName)
        {
            if ((table == null) || (string.IsNullOrEmpty(columnName)))
                return string.Empty;

            SQLFormatter formatter = new SQLFormatter(table);
            return formatter.SqlIdentifier(columnName);
        }
    }
}
