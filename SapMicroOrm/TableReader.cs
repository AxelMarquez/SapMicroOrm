using FastMember;
using SAP.Middleware.Connector;
using SapMicroOrm.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapMicroOrm
{
    public static class TableReaderExtensions
    {
        /// <summary>
        /// Specifies the SAP table to select from
        /// </summary>
        public static TableReader<T> From<T>(this RfcDestination conn) where T : class
        {
            if (typeof(T).IsValueType || typeof(T) == typeof(string))
            {
                //Type is a struct (or primitive)
                //parsedRow = Convert.ChangeType(row.First(), rowType);
                throw new ArgumentException("Selecting lists of structs or primitives is not supported, please select lists of classes.");
            }

            return new TableReader<T>(conn, GetTableName<T>());
        }

        public static string GetTableName<T>()
        {
            var tableAlias = typeof(T).GetCustomAttributes(typeof(Alias), true).FirstOrDefault() as Alias;
            var tableName = tableAlias?._Alias ?? typeof(T).Name;

            return tableName;
        }

        public static string GetColumnName(Member column)
        {
            var columnAlias = column.GetMemberAttribute<Alias>();
            var columnName = columnAlias?._Alias ?? column.Name;

            return columnName;
        }

        private static Dictionary<Type, IEnumerable<string>> _keyColumnsCache = new Dictionary<Type, IEnumerable<string>>();
        public static IEnumerable<string> GetKeyColumns<T>(bool applyColumnAliases = true)
        {
            var rowType = typeof(T);

            if (!_keyColumnsCache.ContainsKey(rowType))
            {
                var accesor = TypeAccessor.Create(rowType);

                var keyColumns = accesor
                    .GetMembers()
                    .Where(m => m.IsDefined(typeof(Key)))
                    .Select(m => applyColumnAliases ? GetColumnName(m) : m.Name)
                    .ToList();

                _keyColumnsCache.Add(rowType, keyColumns);
            }

            return _keyColumnsCache[rowType];
        }

        public static string SerializeKeyColumns<T>(this T entity)
        {
            var keyCols = GetKeyColumns<T>();
            return SapExtensions.SerializeProperties(keyCols, entity);
        }
    }

    public class TableReader<T> where T : class
    {
        private readonly RfcDestination _conn;
        private readonly string _tableName;
        private WhereBuilder _condition = new WhereBuilder();
        private readonly Type _rowType = typeof(T);
        private readonly TypeAccessor _accesor;
        private readonly MemberSet _members;
        private readonly Dictionary<string, Member> _memberByName;
        private readonly Dictionary<string, Member> _memberByAlias;
        private readonly int _peakConnectionsLimit;

        private readonly int _batchSize = 2000;

        public TableReader(RfcDestination conn, string tableName)
        {
            _conn = conn;
            _tableName = tableName;
            _accesor = TypeAccessor.Create(_rowType);
            _members = _accesor.GetMembers();

            _memberByName = _members
                .ToDictionary(m => m.Name);

            _memberByAlias = _members
                .ToDictionary(m => TableReaderExtensions.GetColumnName(m));

            _peakConnectionsLimit = Convert.ToInt32(_conn.Parameters[RfcConfigParameters.PeakConnectionsLimit]);
        }

        /// <summary>
        /// Sets the where clause, for example "MATNR = '123456789' AND WERKS = '0051' OR MAKTX LIKE '%spring%'"
        /// <param name="condition">As you would write it in ABAP</param>
        /// </summary>
        public TableReader<T> Where(string condition)
        {
            _condition = new WhereBuilder() { PrefixCondition = condition };
            return this;
        }

        /// <summary>
        /// Very handy to simulate joins, the conditions passed will be joined with ORs
        /// For example:
        /// ( MATNR = '123456789' ) OR ( MATNR = 'ABCD' ) OR ( MATNR = 'HIJK' ) ...
        /// <param name="orConditons">As you would write it in ABAP</param>
        /// </summary>
        public TableReader<T> Where(IEnumerable<string> orConditons)
        {
            //Remove duplicated conditions
            orConditons = orConditons.Distinct();

            _condition = new WhereBuilder() { Conditions = orConditons };
            return this;
        }

        /// <summary>
        /// Very handy to simulate joins, the andCondition joined with AND plus the orConditions joined with ORs
        /// For example:
        /// ( WERKS = '0051' ) AND ( ( MATNR = '123456789' ) OR ( MATNR = 'ABCD' ) ... )
        /// <param name="andCondition">As you would write it in ABAP</param>
        /// <param name="orConditions">As you would write it in ABAP</param>
        /// </summary>
        public TableReader<T> Where(string andCondition, IEnumerable<string> orConditions)
        {
            //Remove duplicated conditions
            orConditions = orConditions.Distinct();

            _condition = new WhereBuilder() { PrefixCondition = andCondition, Conditions = orConditions };
            return this;
        }

        /// <summary>
        /// Provided an array of entities will set the condition to retrieve the same entities based in the primary key
        /// Handy to retrieve the same rows (with same primary keys) of the same table between different SAP systems
        /// </summary>
        public TableReader<T> WhereByKeys(IEnumerable<T> entities)
        {
            //Remove duplicated entries
            entities = entities
                .ToLookup(e => TableReaderExtensions.SerializeKeyColumns(e))
                .Select(e => e.First());

            var keyCols = TableReaderExtensions.GetKeyColumns<T>();

            if (keyCols.IsEmpty())
            {
                throw new ArgumentException($"At least one property of {_rowType.Name} must have the 'Key' attribute.");
            }

            _condition = new WhereBuilder();

            if (keyCols.IsEmpty()) throw new ArgumentException();

            foreach (var row in entities)
            {
                var expression = string.Empty;

                foreach (var keyCol in keyCols)
                {
                    var propVal = _accesor[row, keyCol];
                    expression += $"{keyCol} = " + (propVal.GetType().IsNumber() ? propVal : $"'{propVal}'") + " AND ";
                }

                expression = expression.ReplaceLastOccurrence(" AND ", string.Empty);
                (_condition.Conditions as List<string>).Add(expression);
            }

            return this;
        }

        /// <summary>
        /// Retrieves the specified columns from the SAP table
        /// </summary>
        public List<T> Select(IEnumerable<string> columns)
        {
            var invalidColumns = columns.Where(c => !_memberByAlias.ContainsKey(c));
            if (!invalidColumns.IsEmpty())
            {
                throw new ArgumentException($"The following columns don't exist in the table (did you forget an Alias?): {invalidColumns.Join()}");
            }

            var resultingRows = new List<string[]>();

            var sapWhereClauses = _condition
                .ToWhereBatched(_batchSize)
                //Split by spaces (SAP doesn't like incomplete words per line!)
                .Select(w => w.Split(' '));

            if (sapWhereClauses.IsEmpty())
            {
                //If no conditions are set ensure that we execute once
                sapWhereClauses = new List<string[]>() { new string[] { "" } };
            }

            //Retrieve records parallely
            Parallel.ForEach(sapWhereClauses, new ParallelOptions() { MaxDegreeOfParallelism = _peakConnectionsLimit }, sapWhereClause =>
            {
                //Source: https://stackoverflow.com/questions/20046390/how-to-get-result-of-a-rfc
                var rfc = _conn.Repository.CreateFunction("/BODS/RFC_READ_TABLE2");
                var delimeter = 'Æ';

                //Start filling parameters
                rfc.SetValue("QUERY_TABLE", _tableName);
                rfc.SetValue("DELIMITER", delimeter);

                var fields = rfc.GetTable("FIELDS");
                foreach (var column in columns)
                {
                    fields.Append();
                    fields.SetValue("FIELDNAME", column);
                }

                if (sapWhereClause.Count() == 1 && sapWhereClause.First().IsEmpty())
                {
                    //No conditions, derivatively left blank
                }
                else
                {
                    var options = rfc.GetTable("OPTIONS");
                    foreach (var option in sapWhereClause)
                    {
                        options.Append();
                        options.SetValue("TEXT", option);
                    }
                }

                //Get tresults
                try
                {
                    rfc.Invoke(_conn);
                }
                catch (Exception e)
                {
                    if (e.Message == "FIELD_NOT_VALID")
                    {
                        throw new ArgumentException("A selected column isn't valid, check that all the selected columns are correctly written.");
                    }
                    else if(e.Message == "A dynamically specified column name is unknown.")
                    {
                        throw new ArgumentException("A column in the WHERE clause isn't correctly written or doesn't exist.");
                    }
                    else if (e.Message == "A condition specified dynamically has an unexpected format.")
                    {
                        throw new ArgumentException("There's a syntax error in the WHERE clause, please check.");
                    }

                    throw;
                }


                var rows = Enumerable.Empty<IRfcDataContainer>()
                    .Concat(rfc.GetTable("TBLOUT128"))
                    .Concat(rfc.GetTable("TBLOUT512"))
                    .Concat(rfc.GetTable("TBLOUT2048"))
                    .Concat(rfc.GetTable("TBLOUT8192"))
                    .Concat(rfc.GetTable("TBLOUT30000"))
                    .Select(r => r.GetString("WA").Split(delimeter))
                    .ToList();

                lock (resultingRows)
                {
                    resultingRows.AddRange(rows);
                }
            });

            return resultingRows
                .Select(r =>
                {
                    var parsedRowInstance = _accesor.CreateNew() as T;

                    var i = 0;
                    foreach (var column in columns)
                    {
                        var member = _memberByAlias[column];
                        var value = Convert.ChangeType(r[i].Trim(), member.Type);//Everything is trimmed!
                        _accesor[parsedRowInstance, member.Name] = value;

                        i++;
                    }

                    return parsedRowInstance;

                })
                .ToList();
        }

        /// <summary>
        /// Retrieves the specified columns from the SAP table
        /// </summary>
        public List<T> Select(string csvColumns)
        {
            var columns = csvColumns
                .Split(',')
                .Select(c => c.Trim());

            return Select(columns);
        }

        /// <summary>
        /// Retrieves all the columns from the SAP table
        /// </summary>
        public List<T> SelectAllColumns()
        {
            //It is recomended to select all columns since
            //The RFC that is called internally in ABAP already selects all columns,
            //so other than data load transferred there's no performance gain in selecting only the needed ones

            return Select(_memberByAlias.Keys);
        }
    }

    public class WhereBuilder
    {
        public string PrefixCondition { get; set; } = string.Empty;
        public IEnumerable<string> Conditions { get; set; } = new List<string>();

        public IEnumerable<string> ToWhereBatched(int batchSize)
        {
            var whereClauses = new List<string>();

            for (int i = 0; true; i++)
            {
                var whereClause = ToWhereClause(i * batchSize, batchSize);
                if (whereClause.IsEmpty()) break;

                whereClauses.Add(whereClause);
            }

            return whereClauses;
        }

        public string ToWhereClause(int skip, int take)
        {
            if (skip > Conditions.Count()) return string.Empty;
            if (Conditions.IsEmpty()) return PrefixCondition;

            var whereClause = new StringBuilder();

            if (!PrefixCondition.IsEmpty())
            {
                whereClause.Append($"({PrefixCondition}) AND (");
            }

            var relevantConditions = Conditions
                .Skip(skip)
                .Take(take)
                .ToList();

            foreach (var condition in relevantConditions)
            {
                whereClause.Append($"({condition}) OR ");
            }

            if (PrefixCondition.IsEmpty())
            {
                whereClause.ReplaceLastOccurrence(" OR ", string.Empty);
            }
            else
            {
                whereClause.ReplaceLastOccurrence(" OR ", ")");
            }

            return whereClause.ToString();
        }
    }
}
