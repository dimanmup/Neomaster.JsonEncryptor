using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#nullable disable
namespace Neomaster;
public class JsonEncryptor
{
    private IDataProtector protector;
    private string path;
    private string json;
    private int valueBytesContainerSize;

    private JsonEncryptor()
    {
    }

    private static readonly JsonEncryptor instance = new();

    public static JsonEncryptor Instance => instance;

    public void Init(string protectorPurpose, string path, int valueBytesContainerSize = 1000)
    {
        protector = DataProtectionProvider.Create(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name).CreateProtector(protectorPurpose);

        this.path = path;
        json = File.ReadAllText(path);

        this.valueBytesContainerSize = valueBytesContainerSize;
    }

    private void SetPathValue(dynamic tree, string path, string value)
    {
        string[] pathParts = path.Split(':', 2);

        if (pathParts.Length == 1)
        {
            tree[pathParts[0]] = value;

            return;
        }

        SetPathValue(tree[pathParts[0]], pathParts[1], value);
    }
    
    private string GetPathValue(string path)
    {
        JObject tree = JsonConvert.DeserializeObject<JObject>(json);

        string[] pathParts = path.Split(':');

        JToken node = tree[pathParts[0]];

        for (int i = 1; i < pathParts.Length; i++)
        {
            node = node[pathParts[i]];
        }

        return node.ToString();
    }

    public string UnprotectPathValue(string path, out bool hasBeenProtected)
    {
        return UnprotectValue(GetPathValue(path), out hasBeenProtected);
    }

    public string UnprotectValue(string value)
    {
        return UnprotectValue(value, out _);
    }

    public string UnprotectValue(string value, out bool hasBeenProtected)
    {
        byte[] valueBytes = new byte[valueBytesContainerSize];

        if (Convert.TryFromBase64String(value, valueBytes, out int bytesWritten))
        {
            hasBeenProtected = true;
            return protector.Unprotect(string.Concat(valueBytes.Take(bytesWritten).Select(b => (char)b)));
        }
        else
        {
            hasBeenProtected = false;
            return value;
        }
    }

    public string ProtectPathValue(string path, out bool hasBeenProtected)
    {
        return ProtectValue(GetPathValue(path), out hasBeenProtected);
    }

    public string ProtectValue(string value)
    {
        return ProtectValue(value, out _);
    }

    public string ProtectValue(string value, out bool hasBeenProtected)
    {
        byte[] valueBytes = new byte[valueBytesContainerSize];

        if (Convert.TryFromBase64String(value, valueBytes, out int bytesWritten))
        {
            hasBeenProtected = true;
            return value;
        }
        else
        {
            hasBeenProtected = false;
            return Convert.ToBase64String(protector.Protect(value).Select(c => (byte)c).ToArray());
        }
    }

    public void WriteValueAsProtected(string path)
    {
        string protectedValue = ProtectPathValue(path, out bool valueHasBeenProtected);

        if (valueHasBeenProtected)
        {
            return;
        }

        JObject tree = JsonConvert.DeserializeObject<JObject>(json);

        SetPathValue(tree, path, protectedValue);

        json = JsonConvert.SerializeObject(tree, Formatting.Indented);
    }

    public void SaveAsProtected(params string[] sectionsPaths)
    {
        for (int i = 0; i < sectionsPaths.Length; i++)
        {
            WriteValueAsProtected(sectionsPaths[i]);
        }

        File.WriteAllText(path, json);
    }
}