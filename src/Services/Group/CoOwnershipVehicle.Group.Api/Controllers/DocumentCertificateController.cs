using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Helpers;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/document")]
public class DocumentCertificateController : BaseAuthenticatedController
{
    private readonly IDocumentService _documentService;

    public DocumentCertificateController(IDocumentService documentService, ILogger<DocumentCertificateController> logger)
        : base(logger)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Verify certificate authenticity by certificate ID (returns HTML page)
    /// </summary>
    [HttpGet("verify-certificate/{certificateId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyCertificate(string certificateId, [FromQuery] string? hash = null)
    {
        try
        {
            var result = await _documentService.VerifyCertificateAsync(certificateId, hash);

            // Return HTML page for better user experience
            var html = HtmlTemplates.GenerateVerificationHtml(result);
            return Content(html, "text/html");
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Certificate not found: {CertificateId}", certificateId);
            var errorHtml = HtmlTemplates.GenerateErrorHtml("Certificate Not Found", ex.Message, certificateId);
            return Content(errorHtml, "text/html");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error verifying certificate: {CertificateId}", certificateId);
            var errorHtml = HtmlTemplates.GenerateErrorHtml("Verification Error", "An error occurred while verifying the certificate", certificateId);
            return Content(errorHtml, "text/html");
        }
    }

    /// <summary>
    /// Verify certificate and return JSON (for API calls)
    /// </summary>
    [HttpGet("verify-certificate-json/{certificateId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyCertificateJson(string certificateId, [FromQuery] string? hash = null)
    {
        try
        {
            var result = await _documentService.VerifyCertificateAsync(certificateId, hash);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Certificate not found: {CertificateId}", certificateId);
            return NotFound(new { error = ex.Message, certificateId });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error verifying certificate: {CertificateId}", certificateId);
            return StatusCode(500, new { error = "An error occurred while verifying the certificate", certificateId });
        }
    }

    /// <summary>
    /// Get signing certificate for a fully signed document
    /// </summary>
    [HttpGet("{documentId}/certificate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetSigningCertificate(Guid documentId)
    {
        try
        {
            var userId = GetUserId();

            // Get base URL for QR code
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

            var result = await _documentService.GetSigningCertificateAsync(documentId, userId, baseUrl);

            var fileName = $"Certificate_{result.FileName}_{result.CertificateId}.pdf";

            return File(result.CertificatePdf, "application/pdf", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for certificate");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to certificate");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Document not fully signed");
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating certificate");
            return StatusCode(500, new { error = "An error occurred while generating the certificate" });
        }
    }
}
