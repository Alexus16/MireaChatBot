using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Npgsql;

namespace MireaChatBot.Postgres
{
    public class PostgresClient
    {
        private string _connString;

        private PostgresClient(string connString)
        {
            _connString = connString;
        }

        public static PostgresClient CreateClient(PostgresConnectionData connectionData)
        {
            return new PostgresClient(connectionData.GetConnectionString());
        }

        private NpgsqlConnection connectToDB()
        {
            return new NpgsqlConnection(_connString);
        }

        private NpgsqlCommand createCommand()
        {
            return null;
        }
        private void disconnectFromDB(NpgsqlConnection conn)
        {
            conn.Close();
        }
    }

    interface CommandExpression
    {
        string Interpret();
    }

    public interface ValueExpression
    {
        string GetValue();
    }

    public class StringExpression : ValueExpression
    {
        private string _value;

        public StringExpression(string value)
        {
            _value = value;
        }

        public static implicit operator StringExpression(string str)
        {
            return new StringExpression(str);
        }

        public string GetValue()
        {
            return $"'{_value}'";
        }
    }


    public class JsonObjectExpression : ValueExpression
    {
        private object _value;

        public JsonObjectExpression(object value)
        {
            _value = value;
        }

        public string GetValue()
        {
            return _value.ToString();
        }
    }

    class SelectExpression : CommandExpression
    {
        private string _tableName;
        private List<string> _fields;
        private ConditionExpression _conditionExpression;
        public SelectExpression(string tableName, IEnumerable<string> fields = null)
        {
            _tableName = tableName;
            _fields = fields?.ToList() ?? new List<string>();
        }

        public void AddField(string fieldName)
        {
            if(!_fields.Contains(fieldName))
            {
                _fields.Add(fieldName);
            }
        }

        public void SetCondition(ConditionExpression conditionExpression)
        {
            _conditionExpression = conditionExpression;
        }

        public string Interpret()
        {
            if (_fields.Count == 0)
            {
                throw new InvalidOperationException("No field specified");
            }
            if(string.IsNullOrWhiteSpace(_tableName))
            {
                throw new InvalidOperationException("No table name specified");
            }
            StringBuilder builder = new StringBuilder();
            builder.Append("SELECT (");
            
            for (int i = 0; i < _fields.Count - 1; i++)
            {
                builder.Append(_fields[i]);
                builder.Append(", ");
            }
            builder.Append(_fields.Last());
            builder.Append(") FROM ");
            builder.Append(_tableName);
            if(!(_conditionExpression is null))
            {
                builder.Append(" WHERE ");
                builder.Append(_conditionExpression.Interpret());
            }
            return builder.ToString();
        }
    }

    public class UpdateExpression : CommandExpression
    {
        private string _tableName;
        private List<(string, ValueExpression)> _fieldsAndValues;
        private ConditionExpression _conditionExpression;

        public UpdateExpression(string tableName, IEnumerable<(string, ValueExpression)> fieldsAndValues = null)
        {
            _tableName = tableName;
            _fieldsAndValues = fieldsAndValues?.ToList() ?? new List<(string, ValueExpression)>();
        }

        public void AddFieldValue(string fieldName, ValueExpression value)
        {
            _fieldsAndValues.Add((fieldName, value));
        }

        public void SetCondition(ConditionExpression expression)
        {
            _conditionExpression = expression;
        }

        public string Interpret()
        {
            if(string.IsNullOrWhiteSpace(_tableName))
            {
                throw new InvalidOperationException("No table name specified");
            }
            if(_fieldsAndValues.Count == 0)
            {
                throw new InvalidOperationException("No values to update specified");
            }
            if (_conditionExpression is null)
            {
                throw new InvalidOperationException("No condition specified");
            }
            StringBuilder builder = new StringBuilder();
            builder.Append("UPDATE ");
            builder.Append(_tableName);
            builder.Append(" SET ");
            for(int i = 0; i < _fieldsAndValues.Count - 1; i++)
            {
                builder.Append($"{_fieldsAndValues[i].Item1}={_fieldsAndValues[i].Item2.GetValue()},");
            }
            builder.Append($"{_fieldsAndValues.Last().Item1}={_fieldsAndValues.Last().Item2.GetValue()}");
            builder.Append(" WHERE ");
            builder.Append(_conditionExpression.Interpret());
            return builder.ToString();
        }
    }

    public class InsertExpression : CommandExpression
    {
        private string _tableName;
        private List<(string, ValueExpression)> _fieldsAndValues;

        public string Interpret()
        {
            if(string.IsNullOrWhiteSpace(_tableName))
            {
                throw new InvalidOperationException("No table name specified");
            }
            if(_fieldsAndValues.Count == 0)
            {
                throw new InvalidOperationException("No data specified");
            }
            StringBuilder builder = new StringBuilder();
            builder.Append("INSERT INTO ");
            builder.Append(_tableName);
            builder.Append(" (");
            StringBuilder fieldBuilder = new StringBuilder();
            fieldBuilder.Append("(");
            StringBuilder valueBuilder = new StringBuilder();
            valueBuilder.Append("(");
            for (int i = 0; i < _fieldsAndValues.Count - 1; i++)
            {
                fieldBuilder.Append($"{_fieldsAndValues[i].Item1}, ");
                valueBuilder.Append($"{_fieldsAndValues[i].Item2.GetValue()}, ");
            }
            fieldBuilder.Append($"{_fieldsAndValues.Last().Item1})");
            valueBuilder.Append($"{_fieldsAndValues.Last().Item2.GetValue()})");
            builder.Append($"{fieldBuilder.ToString()} VALUES {valueBuilder.ToString()}");
            return builder.ToString();
        }
    }

    public class DeleteExpression : CommandExpression
    {
        private string _tableName;
        private ConditionExpression _conditionExpression;
        public DeleteExpression(string tableName)
        {
            _tableName = tableName;
        }

        public void SetCondition(ConditionExpression conditionExpression)
        {
            _conditionExpression = conditionExpression;
        }

        public string Interpret()
        {
            if(string.IsNullOrWhiteSpace(_tableName))
            {
                throw new InvalidOperationException("No table name specified");
            }
            StringBuilder builder = new StringBuilder();
            builder.Append("DELETE FROM ");
            builder.Append(_tableName);
            builder.Append(" WHERE ");
            builder.Append(_conditionExpression.Interpret());
            return builder.ToString();
        }
    }

    public interface ConditionExpression
    {
        string Interpret();
    }

    public enum CommandAction
    {
        INSERT,
        UPDATE,
        DELETE,
        SELECT,
    }

    public class PostgresConnectionData
    {
        private string _host;
        private string _port;
        private string _username;
        private string _password;
        private string _dbName;

        public PostgresConnectionData(string host, string port, string username, string password, string dbName)
        {
            _host = host;
            _port = port;
            _username = username;
            _password = password;
            _dbName = dbName;
        }

        public string GetConnectionString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append($"Host={_host}:{_port};");
            builder.Append($"Username={_username};");
            builder.Append($"Password={_password};");
            builder.Append($"Database={_dbName}");
            return builder.ToString();
        }
        public string Host => _host;
        public string Port => _port;
        public string Username => _username;
        public string Password => _password;
        public string DBName => _dbName;

    }
}
