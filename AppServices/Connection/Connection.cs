using AppServices.Security;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AppServices
{
    public class Connection
    {
#if DEBUG
    ~Connection() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

        public static string CurrentConnectionString
        {
            get
            {
                string storedValue = Settings.Default.ConnectionString;
                if (string.IsNullOrEmpty(storedValue))
                    return string.Empty;

                if (StringProtector.IsEncrypted(storedValue))
                {
                    if (!StringProtector.TryDecrypt(storedValue, out string value))
                        return string.Empty;
                    if (storedValue.StartsWith("dpapi:v1:", StringComparison.Ordinal))
                        SetConnectionString(value);
                    return value;
                }

                // Upgrade connection strings saved by earlier versions in plain text.
                SetConnectionString(storedValue);
                return storedValue;
            }
        }

        public static void TestConnection()
        {
            Flags.ServerAttivo = false;
            try
            {
                string connectionString = CurrentConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString))
                    return;

                using SqlConnection connection = new(connectionString);
                connection.Open();
                Flags.ServerAttivo = connection.State == ConnectionState.Open;
            }
            catch { Flags.ServerAttivo = false; }
        }

        public static bool SetConnectionString(string? connectionstring)
        {
            if (connectionstring is null)
                return false;

            Settings.Default.ConnectionString = string.IsNullOrEmpty(connectionstring)
                ? string.Empty
                : StringProtector.Encrypt(connectionstring);
            Settings.Default.Save();
            return true;
        }
    }
}