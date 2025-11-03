using CoOwnershipVehicle.Group.Api.DTOs;

namespace CoOwnershipVehicle.Group.Api.Helpers;

public static class HtmlTemplates
{
    public static string GenerateVerificationHtml(CertificateVerificationResult result)
    {
        var statusColor = result.IsValid ? "#10b981" : "#ef4444";
        var statusIcon = result.IsValid ? "✓" : "✗";
        var statusText = result.IsValid ? "VALID CERTIFICATE" : "INVALID CERTIFICATE";

        var warnings = new List<string>();
        if (result.IsRevoked) warnings.Add($"⚠️ Revoked: {result.RevocationReason}");
        if (result.IsExpired) warnings.Add($"⚠️ Expired on {result.ExpiresAt:yyyy-MM-dd}");
        if (!result.HashMatches) warnings.Add("⚠️ Document hash does not match");

        var warningsHtml = warnings.Any()
            ? $"<div style='background: #fef3c7; border-left: 4px solid #f59e0b; padding: 12px; margin: 20px 0;'>{string.Join("<br>", warnings)}</div>"
            : "";

        var signersHtml = string.Join("", result.Signers.Select((s, i) => $@"
            <div style='background: #f9fafb; border-radius: 8px; padding: 12px; margin-bottom: 10px;'>
                <div style='font-weight: 600; color: #1f2937;'>✍️ Signer {i + 1}: {s.SignerName}</div>
                <div style='font-size: 14px; color: #6b7280; margin-top: 4px;'>
                    📧 {s.SignerEmail}<br>
                    📅 {s.SignedAt:yyyy-MM-dd HH:mm:ss} UTC<br>
                    🌐 IP: {s.IpAddress}<br>
                    💻 Device: {s.DeviceInfo}
                </div>
            </div>
        "));

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Certificate Verification - {result.CertificateId}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; min-height: 100vh; }}
        .container {{ max-width: 800px; margin: 0 auto; background: white; border-radius: 16px; box-shadow: 0 20px 60px rgba(0,0,0,0.3); overflow: hidden; }}
        .header {{ background: {statusColor}; color: white; padding: 30px; text-align: center; }}
        .header h1 {{ font-size: 32px; margin-bottom: 10px; }}
        .status-icon {{ font-size: 64px; margin-bottom: 10px; }}
        .content {{ padding: 30px; }}
        .section {{ margin-bottom: 25px; }}
        .section-title {{ font-size: 18px; font-weight: 600; color: #1f2937; margin-bottom: 12px; border-bottom: 2px solid #e5e7eb; padding-bottom: 8px; }}
        .info-row {{ display: flex; padding: 10px 0; border-bottom: 1px solid #f3f4f6; }}
        .info-label {{ font-weight: 600; color: #6b7280; min-width: 150px; }}
        .info-value {{ color: #1f2937; flex: 1; }}
        .badge {{ display: inline-block; padding: 4px 12px; border-radius: 12px; font-size: 12px; font-weight: 600; }}
        .badge-success {{ background: #d1fae5; color: #065f46; }}
        .badge-danger {{ background: #fee2e2; color: #991b1b; }}
        .footer {{ background: #f9fafb; padding: 20px; text-align: center; font-size: 14px; color: #6b7280; }}
        .btn {{ display: inline-block; padding: 12px 24px; background: #3b82f6; color: white; text-decoration: none; border-radius: 8px; font-weight: 600; margin: 5px; transition: all 0.3s; }}
        .btn:hover {{ background: #2563eb; transform: translateY(-2px); box-shadow: 0 4px 12px rgba(59,130,246,0.4); }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='status-icon'>{statusIcon}</div>
            <h1>{statusText}</h1>
            <p>{result.CertificateId}</p>
        </div>

        {warningsHtml}

        <div class='content'>
            <div class='section'>
                <div class='section-title'>📄 Document Information</div>
                <div class='info-row'>
                    <div class='info-label'>Document Name:</div>
                    <div class='info-value'>{result.DocumentName}</div>
                </div>
                <div class='info-row'>
                    <div class='info-label'>Document ID:</div>
                    <div class='info-value'>{result.DocumentId}</div>
                </div>
                <div class='info-row'>
                    <div class='info-label'>Total Signers:</div>
                    <div class='info-value'>{result.TotalSigners} {(result.TotalSigners > 1 ? "signers" : "signer")}</div>
                </div>
            </div>

            <div class='section'>
                <div class='section-title'>🔒 Certificate Details</div>
                <div class='info-row'>
                    <div class='info-label'>Generated:</div>
                    <div class='info-value'>{result.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</div>
                </div>
                <div class='info-row'>
                    <div class='info-label'>Expires:</div>
                    <div class='info-value'>{(result.ExpiresAt.HasValue ? result.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "Never")}</div>
                </div>
                <div class='info-row'>
                    <div class='info-label'>Verified:</div>
                    <div class='info-value'>{result.VerifiedAt:yyyy-MM-dd HH:mm:ss} UTC</div>
                </div>
                <div class='info-row'>
                    <div class='info-label'>Hash Matches:</div>
                    <div class='info-value'>
                        <span class='badge {(result.HashMatches ? "badge-success" : "badge-danger")}'>
                            {(result.HashMatches ? "✓ Yes" : "✗ No")}
                        </span>
                    </div>
                </div>
            </div>

            <div class='section'>
                <div class='section-title'>✍️ Signatures ({result.TotalSigners})</div>
                {signersHtml}
            </div>

            <div style='text-align: center; margin-top: 30px;'>
                <a href='/swagger' class='btn'>📚 View API Documentation</a>
                <a href='{result.VerificationUrl}-json?hash={Uri.EscapeDataString(result.DocumentName)}' class='btn'>📊 JSON Response</a>
            </div>
        </div>

        <div class='footer'>
            <p>🔐 This certificate was verified using the Co-Ownership Vehicle Management System</p>
            <p style='margin-top: 8px; font-size: 12px;'>Certificate verification performed on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string GenerateErrorHtml(string title, string message, string certificateId)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 20px; min-height: 100vh; display: flex; align-items: center; justify-content: center; }}
        .container {{ max-width: 600px; background: white; border-radius: 16px; box-shadow: 0 20px 60px rgba(0,0,0,0.3); overflow: hidden; text-align: center; padding: 40px; }}
        .error-icon {{ font-size: 80px; margin-bottom: 20px; }}
        h1 {{ font-size: 28px; color: #ef4444; margin-bottom: 15px; }}
        p {{ color: #6b7280; font-size: 16px; line-height: 1.6; margin-bottom: 10px; }}
        .cert-id {{ background: #f3f4f6; padding: 12px; border-radius: 8px; font-family: monospace; margin: 20px 0; word-break: break-all; }}
        .btn {{ display: inline-block; padding: 12px 24px; background: #3b82f6; color: white; text-decoration: none; border-radius: 8px; font-weight: 600; margin-top: 20px; }}
        .btn:hover {{ background: #2563eb; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error-icon'>❌</div>
        <h1>{title}</h1>
        <p>{message}</p>
        {(string.IsNullOrEmpty(certificateId) ? "" : $"<div class='cert-id'>Certificate ID: {certificateId}</div>")}
        <a href='/swagger' class='btn'>Go to API Documentation</a>
    </div>
</body>
</html>";
    }
}
