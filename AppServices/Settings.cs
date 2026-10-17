using System.Xml;
using System.Xml.Linq;
namespace AppServices;
/// <summary>NativeAOT user settings retaining Settings.Default/Save without reflected providers.</summary>
internal sealed class Settings
{
#if DEBUG
    ~Settings() => global::Leonardo18.Diagnostics.GcDebugLog.Collected(this);
#endif

    private readonly object _sync = new();
    private readonly string SettingsDirectory;
    private readonly string SettingsPath;
    public static Settings Default { get; } = new();
    private string _connectionString;
    private Settings() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Leonardo18")) { }
    internal Settings(string directory)
    {
        SettingsDirectory = directory;
        SettingsPath = Path.Combine(directory, "user.config");
        _connectionString = Load();
    }
    public string ConnectionString
    {
        get { lock (_sync) return _connectionString; }
        set { ArgumentNullException.ThrowIfNull(value); lock (_sync) _connectionString = value; }
    }
    public void Save()
    {
        lock (_sync)
        {
            Directory.CreateDirectory(SettingsDirectory);
            var document = new XDocument(new XElement("configuration", new XElement("userSettings",
                new XElement("AppServices.Settings", new XElement("setting",
                    new XAttribute("name", "ConnectionString"), new XAttribute("serializeAs", "String"),
                    new XElement("value", _connectionString))))));
            string temporaryPath = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { document.Save(temporaryPath); File.Move(temporaryPath, SettingsPath, overwrite: true); }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }
    }
    private string Load()
    {
        if (File.Exists(SettingsPath)) return ReadValue(SettingsPath) ?? "";
        if (!Directory.Exists(SettingsDirectory)) return "";
        try
        {
            foreach (string path in Directory.EnumerateFiles(SettingsDirectory, "user.config", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                string? value = ReadValue(path);
                if (value is not null) return value;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return "";
    }
    private static string? ReadValue(string path)
    {
        try
        {
            var document = XDocument.Load(path);
            return document.Root?.Element("userSettings")?.Element("AppServices.Settings")?
                .Elements("setting").FirstOrDefault(element => (string?)element.Attribute("name") == "ConnectionString")?
                .Element("value")?.Value;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (XmlException) { return null; }
    }
}