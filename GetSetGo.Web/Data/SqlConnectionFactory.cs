using Microsoft.Data.SqlClient;

namespace GetSetGo.Web.Data;

public interface ISqlConnectionFactory { SqlConnection Create(); }

public sealed class SqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    public SqlConnection Create() => new(configuration.GetConnectionString("GetSetGoDb")
        ?? throw new InvalidOperationException("Connection string 'GetSetGoDb' is missing."));
}
