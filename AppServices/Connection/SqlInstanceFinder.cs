using Microsoft.Data.Sql;
using System.Data;

namespace AppServices
{
    public static class SqlInstanceFinder
    {
        public static List<string> GetInstances()
        {
            var result = new List<string>();

            DataTable table = SqlDataSourceEnumerator.Instance.GetDataSources();

            foreach (DataRow row in table.Rows)
            {
                string? server = row.Field<string>("ServerName");
                string? instance = row.Field<string>("InstanceName");

                if (string.IsNullOrWhiteSpace(server))
                    continue;

                result.Add(string.IsNullOrWhiteSpace(instance)
                    ? server
                    : $"{server}\\{instance}");
            }

            return result;
        }
    }
}
