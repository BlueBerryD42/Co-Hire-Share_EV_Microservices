using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;

namespace CoOwnershipVehicle.Shared.Configuration;

public class EnvFileConfigurationProvider : ConfigurationProvider
{
    private readonly string _filePath;

    public EnvFileConfigurationProvider(string filePath)
    {
        _filePath = filePath;
    }

    public override void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        Data = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);

        var lines = File.ReadAllLines(_filePath);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
            {
                continue;
            }

            var parts = trimmedLine.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim().Replace("__", ":"); // Chuyển đổi QrCode__EncryptionKey thành QrCode:EncryptionKey
            var value = parts[1].Trim();

            // Gỡ bỏ dấu ngoặc kép nếu có
            if (value.Length > 1 && value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value.Substring(1, value.Length - 2);
            }

            Data[key] = value;
        }
    }
}