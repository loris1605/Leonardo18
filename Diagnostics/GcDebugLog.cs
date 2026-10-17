#if DEBUG
namespace Leonardo18.Diagnostics;
/// <summary>Diagnostic-only finalization logging. Never stores or inspects object data.</summary>
internal static class GcDebugLog
{
    internal const string Prefix = "***** [GC] PID=";
    public static void Collected(object instance)
    {
        try
        {
            int processId = global::System.Environment.ProcessId;
            int instanceId = global::System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(instance);
            string message = $"{Prefix}{processId} {instance.GetType().FullName} {instanceId} DISTRUTTO *****";
            global::System.Diagnostics.Debug.WriteLine(message);
            if (global::System.AppDomain.CurrentDomain.FriendlyName == "Leonardo18.DatabaseHost")
                global::System.Console.Error.WriteLine(message);
        }
        catch
        {
            // Diagnostics must never throw from the finalizer thread.
        }
    }
}
#endif