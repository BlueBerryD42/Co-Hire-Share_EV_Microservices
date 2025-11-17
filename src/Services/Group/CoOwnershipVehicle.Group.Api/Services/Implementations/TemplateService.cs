using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class TemplateService : ITemplateService
{
    private readonly GroupDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<TemplateService> _logger;
    private readonly IUserServiceClient _userServiceClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TemplateService(
        GroupDbContext context,
        IFileStorageService fileStorage,
        ILogger<TemplateService> logger,
        IUserServiceClient userServiceClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _fileStorage = fileStorage;
        _logger = logger;
        _userServiceClient = userServiceClient;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetAccessToken()
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            throw new UnauthorizedAccessException("Missing or invalid authorization header");
        }
        return authHeader.Substring("Bearer ".Length).Trim();
    }

    public async Task<TemplateDetailResponse> CreateTemplateAsync(CreateTemplateRequest request, Guid userId)
    {
        _logger.LogInformation("Creating new template '{Name}' by user {UserId}", request.Name, userId);

        // Verify user is system admin via HTTP
        var accessToken = GetAccessToken();
        var user = await _userServiceClient.GetUserAsync(userId, accessToken);

        _logger.LogInformation("User lookup result - UserId: {UserId}, Found: {Found}, Role: {Role}",
            userId,
            user != null,
            user?.Role.ToString() ?? "NULL");

        if (user == null)
        {
            _logger.LogWarning("User not found with ID: {UserId}", userId);
            throw new UnauthorizedAccessException("User not found");
        }

        if (user.Role != UserRole.SystemAdmin)
        {
            _logger.LogWarning("User {UserId} with role {Role} attempted to create template. SystemAdmin required.",
                userId, user.Role);
            throw new UnauthorizedAccessException($"Only system administrators can create templates. Your role: {user.Role}");
        }

        _logger.LogInformation("User {UserId} authorized as SystemAdmin", userId);

        try
        {
            // Validate template variables
            _logger.LogInformation("Validating template variables...");
            var variableNames = request.Variables?.Select(v => v.Name).ToList() ?? new List<string>();
            var templateContent = request.TemplateContent;

            // Extract variables from template content
            var contentVariables = ExtractVariablesFromTemplate(templateContent);
            var missingVariables = contentVariables.Except(variableNames).ToList();

            if (missingVariables.Any())
            {
                _logger.LogWarning("Template contains undefined variables: {Variables}", string.Join(", ", missingVariables));
                throw new ArgumentException($"Template contains undefined variables: {string.Join(", ", missingVariables)}");
            }

            // Serialize variables
            _logger.LogInformation("Serializing template variables...");
            string variablesJson;
            try
            {
                variablesJson = JsonSerializer.Serialize(request.Variables ?? new List<TemplateVariable>());
                _logger.LogInformation("Variables JSON: {Json}", variablesJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize variables");
                throw new InvalidOperationException("Failed to serialize template variables", ex);
            }

            // Create template
            _logger.LogInformation("Creating template entity...");
            var template = new DocumentTemplate
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                TemplateContent = templateContent,
                VariablesJson = variablesJson,
                IsActive = request.IsActive,
                CreatedBy = userId,
                Version = 1,
                PreviewImageUrl = request.PreviewImageUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Adding template to context...");
            _context.DocumentTemplates.Add(template);

            _logger.LogInformation("Saving changes to database...");
            await _context.SaveChangesAsync();

            _logger.LogInformation("Template {TemplateId} created successfully", template.Id);

            return await MapToDetailResponse(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<List<TemplateListResponse>> GetTemplatesAsync(TemplateQueryParameters parameters)
    {
        var query = _context.DocumentTemplates
            .Include(t => t.Creator)
            .AsQueryable();

        // Apply filters
        if (parameters.Category.HasValue)
        {
            query = query.Where(t => t.Category == parameters.Category.Value);
        }

        if (parameters.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == parameters.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var searchTerm = parameters.SearchTerm.ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(searchTerm) ||
                (t.Description != null && t.Description.ToLower().Contains(searchTerm)));
        }

        var templates = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return templates.Select(MapToListResponse).ToList();
    }

    public async Task<TemplateDetailResponse> GetTemplateByIdAsync(Guid templateId)
    {
        var template = await _context.DocumentTemplates
            .Include(t => t.Creator)
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template == null)
        {
            throw new KeyNotFoundException($"Template with ID {templateId} not found");
        }

        return await MapToDetailResponse(template);
    }

    public async Task<TemplateDetailResponse> UpdateTemplateAsync(Guid templateId, CreateTemplateRequest request, Guid userId)
    {
        _logger.LogInformation("Updating template {TemplateId} by user {UserId}", templateId, userId);

        // Verify user is system admin via HTTP
        var accessToken = GetAccessToken();
        var user = await _userServiceClient.GetUserAsync(userId, accessToken);
        if (user == null || user.Role != UserRole.SystemAdmin)
        {
            throw new UnauthorizedAccessException("Only system administrators can update templates");
        }

        var template = await _context.DocumentTemplates.FindAsync(templateId);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template with ID {templateId} not found");
        }

        // Update template
        template.Name = request.Name;
        template.Description = request.Description;
        template.Category = request.Category;
        template.TemplateContent = request.TemplateContent;
        template.VariablesJson = JsonSerializer.Serialize(request.Variables);
        template.IsActive = request.IsActive;
        template.PreviewImageUrl = request.PreviewImageUrl;
        template.Version += 1;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Template {TemplateId} updated to version {Version}", templateId, template.Version);

        return await MapToDetailResponse(template);
    }

    public async Task DeleteTemplateAsync(Guid templateId, Guid userId)
    {
        _logger.LogInformation("Deleting template {TemplateId} by user {UserId}", templateId, userId);

        // Verify user is system admin via HTTP
        var accessToken = GetAccessToken();
        var user = await _userServiceClient.GetUserAsync(userId, accessToken);
        if (user == null || user.Role != UserRole.SystemAdmin)
        {
            throw new UnauthorizedAccessException("Only system administrators can delete templates");
        }

        var template = await _context.DocumentTemplates.FindAsync(templateId);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template with ID {templateId} not found");
        }

        // Check if template is being used
        var usageCount = await _context.Documents.CountAsync(d => d.TemplateId == templateId);
        if (usageCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete template that has been used to generate {usageCount} document(s). Deactivate it instead.");
        }

        _context.DocumentTemplates.Remove(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Template {TemplateId} deleted successfully", templateId);
    }

    public async Task<GenerateFromTemplateResponse> GenerateFromTemplateAsync(GenerateFromTemplateRequest request, Guid userId)
    {
        _logger.LogInformation("Generating document from template {TemplateId} for group {GroupId}",
            request.TemplateId, request.GroupId);

        // Get template
        var template = await _context.DocumentTemplates.FindAsync(request.TemplateId);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template with ID {request.TemplateId} not found");
        }

        if (!template.IsActive)
        {
            throw new InvalidOperationException("Cannot generate documents from inactive templates");
        }

        // Verify user is group member
        var isGroupMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == userId);

        if (!isGroupMember)
        {
            throw new UnauthorizedAccessException("User is not a member of the specified group");
        }

        // Parse template variables
        var templateVariables = JsonSerializer.Deserialize<List<TemplateVariable>>(template.VariablesJson) ?? new List<TemplateVariable>();

        // Validate all required variables are provided
        var requiredVariables = templateVariables.Where(v => v.Required).Select(v => v.Name).ToList();
        var providedVariables = request.VariableValues.Keys.ToList();
        var missingVariables = requiredVariables.Except(providedVariables).ToList();

        if (missingVariables.Any())
        {
            throw new ArgumentException($"Missing required variables: {string.Join(", ", missingVariables)}");
        }

        // Substitute variables in template content
        _logger.LogInformation("Template content length before substitution: {Length}", template.TemplateContent.Length);
        _logger.LogInformation("Template content preview: {Preview}", template.TemplateContent.Substring(0, Math.Min(300, template.TemplateContent.Length)));
        _logger.LogInformation("Variable values: {Values}", JsonSerializer.Serialize(request.VariableValues));

        var processedContent = SubstituteVariables(template.TemplateContent, request.VariableValues);

        _logger.LogInformation("Processed content length after substitution: {Length}", processedContent.Length);
        _logger.LogInformation("Processed content preview: {Preview}", processedContent.Substring(0, Math.Min(300, processedContent.Length)));

        // Generate PDF from HTML
        var fileName = request.CustomFileName ?? $"{template.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".pdf";
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = await GeneratePdfFromHtmlAsync(processedContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF from template {TemplateId}", request.TemplateId);
            throw new InvalidOperationException("Failed to generate PDF document", ex);
        }

        // Upload to storage
        var storageKey = $"documents/{request.GroupId}/{Guid.NewGuid()}.pdf";
        using var pdfStream = new MemoryStream(pdfBytes);
        await _fileStorage.UploadFileAsync(pdfStream, storageKey, "application/pdf");

        // Compute hash
        pdfStream.Position = 0;
        var fileHash = await ComputeFileHashAsync(pdfStream);

        // Create document record
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = request.GroupId,
            Type = DocumentType.Other,
            StorageKey = storageKey,
            FileName = fileName,
            FileSize = pdfBytes.Length,
            ContentType = "application/pdf",
            FileHash = fileHash,
            SignatureStatus = SignatureStatus.Draft,
            Description = request.Description ?? $"Generated from template: {template.Name}",
            TemplateId = template.Id,
            TemplateVariablesJson = JsonSerializer.Serialize(request.VariableValues),
            UploadedBy = userId,
            IsVirusScanned = true,
            VirusScanPassed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document {DocumentId} generated from template {TemplateId}", document.Id, template.Id);

        // Generate download URL
        var downloadUrl = await _fileStorage.GetSecureUrlAsync(storageKey);

        return new GenerateFromTemplateResponse
        {
            DocumentId = document.Id,
            FileName = fileName,
            FileSize = pdfBytes.Length,
            DownloadUrl = downloadUrl,
            CreatedAt = document.CreatedAt
        };
    }

    public async Task<TemplatePreviewResponse> PreviewTemplateAsync(Guid templateId, Dictionary<string, string> variableValues)
    {
        var template = await _context.DocumentTemplates.FindAsync(templateId);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template with ID {templateId} not found");
        }

        // Get all variables in template
        var contentVariables = ExtractVariablesFromTemplate(template.TemplateContent);
        var providedVariables = variableValues.Keys.ToList();
        var missingVariables = contentVariables.Except(providedVariables).ToList();

        // Substitute provided variables, leave missing ones as placeholders
        var previewContent = SubstituteVariables(template.TemplateContent, variableValues);

        return new TemplatePreviewResponse
        {
            PreviewHtml = previewContent,
            MissingVariables = missingVariables
        };
    }

    public async Task SeedTemplatesAsync()
    {
        _logger.LogInformation("Seeding pre-built document templates");

        // Check if templates already exist
        var existingCount = await _context.DocumentTemplates.CountAsync();
        if (existingCount > 0)
        {
            _logger.LogInformation("Templates already seeded, skipping");
            return;
        }

        // For seeding, use a default system admin ID (Guid.Empty or a known admin ID)
        // In production, this should be configured via environment variable or configuration
        var systemAdminId = Guid.Empty; // Default for seeding - templates will be created with this ID
        
        // Try to get access token if available (for HTTP context)
        string? accessToken = null;
        try
        {
            accessToken = GetAccessToken();
            // Try to find a system admin via User Service
            // Note: This requires an active HTTP context with authentication
            // If not available, use default ID
        }
        catch
        {
            _logger.LogWarning("No HTTP context available for seeding templates, using default admin ID");
        }

        // If we have access token, try to find a system admin user
        if (accessToken != null)
        {
            // Note: We would need to list users and find a system admin
            // For now, use default ID for seeding
            _logger.LogInformation("Using default system admin ID for template seeding");
        }

        var templates = GetPreBuiltTemplates(systemAdminId);

        _context.DocumentTemplates.AddRange(templates);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} pre-built templates", templates.Count);
    }

    #region Helper Methods

    private List<DocumentTemplate> GetPreBuiltTemplates(Guid createdBy)
    {
        return new List<DocumentTemplate>
        {
            // 1. Vehicle Co-Ownership Agreement
            CreateCoOwnershipAgreementTemplate(createdBy),

            // 2. Cost-Sharing Agreement
            CreateCostSharingAgreementTemplate(createdBy),

            // 3. Usage Terms and Conditions
            CreateUsageTermsTemplate(createdBy),

            // 4. Maintenance Responsibility Contract
            CreateMaintenanceContractTemplate(createdBy),

            // 5. Vehicle Sale Agreement
            CreateVehicleSaleAgreementTemplate(createdBy)
        };
    }

    private DocumentTemplate CreateCoOwnershipAgreementTemplate(Guid createdBy)
    {
        var variables = new List<TemplateVariable>
        {
            new() { Name = "vehicleModel", Label = "Vehicle Model", Type = "text", Required = true, Placeholder = "Tesla Model 3" },
            new() { Name = "vin", Label = "VIN", Type = "text", Required = true, Placeholder = "5YJSA1E26MF123456" },
            new() { Name = "plateNumber", Label = "License Plate", Type = "text", Required = true },
            new() { Name = "ownerNames", Label = "Owner Names", Type = "text", Required = true, Placeholder = "John Doe, Jane Smith" },
            new() { Name = "sharePercentages", Label = "Share Percentages", Type = "text", Required = true, Placeholder = "50%, 50%" },
            new() { Name = "purchaseDate", Label = "Purchase Date", Type = "date", Required = true },
            new() { Name = "purchasePrice", Label = "Purchase Price", Type = "text", Required = true, Placeholder = "$45,000" },
            new() { Name = "effectiveDate", Label = "Effective Date", Type = "date", Required = true }
        };

        var content = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; padding: 40px; }
        h1 { text-align: center; color: #2c3e50; }
        h2 { color: #34495e; margin-top: 30px; }
        .header { text-align: center; margin-bottom: 40px; }
        .section { margin-bottom: 25px; }
        .signature-block { margin-top: 60px; display: flex; justify-content: space-between; }
        .signature-line { border-top: 1px solid #000; width: 200px; margin-top: 40px; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th, td { border: 1px solid #ddd; padding: 10px; text-align: left; }
        th { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <div class=""header"">
        <h1>VEHICLE CO-OWNERSHIP AGREEMENT</h1>
        <p><strong>Effective Date:</strong> {{effectiveDate}}</p>
    </div>

    <div class=""section"">
        <h2>1. PARTIES & VEHICLE INFORMATION</h2>
        <p>This Agreement is entered into by and between the following co-owners (""Parties""):</p>
        <p><strong>Co-Owners:</strong> {{ownerNames}}</p>
        <p><strong>Vehicle:</strong> {{vehicleModel}}</p>
        <p><strong>VIN:</strong> {{vin}}</p>
        <p><strong>License Plate:</strong> {{plateNumber}}</p>
        <p><strong>Purchase Date:</strong> {{purchaseDate}}</p>
        <p><strong>Purchase Price:</strong> {{purchasePrice}}</p>
    </div>

    <div class=""section"">
        <h2>2. OWNERSHIP STRUCTURE</h2>
        <p>The Parties agree to the following ownership percentages:</p>
        <p><strong>Ownership Shares:</strong> {{sharePercentages}}</p>
        <p>Each Party's ownership interest shall be proportionate to their share percentage.</p>
    </div>

    <div class=""section"">
        <h2>3. USAGE RIGHTS</h2>
        <p>3.1. All Parties shall have equal access to the Vehicle, subject to a fair scheduling system agreed upon by all Parties.</p>
        <p>3.2. The Vehicle shall be maintained in good condition, and no Party shall use the Vehicle for illegal purposes.</p>
        <p>3.3. All Parties must maintain valid driver's licenses and follow all applicable traffic laws.</p>
    </div>

    <div class=""section"">
        <h2>4. FINANCIAL OBLIGATIONS</h2>
        <p>4.1. All costs related to the Vehicle (insurance, maintenance, repairs, registration, etc.) shall be shared proportionately according to ownership percentages.</p>
        <p>4.2. Each Party shall contribute their share of expenses within 30 days of being notified.</p>
        <p>4.3. Late payments may be subject to interest charges as agreed upon by all Parties.</p>
    </div>

    <div class=""section"">
        <h2>5. MAINTENANCE & REPAIRS</h2>
        <p>5.1. Routine maintenance shall be performed according to the manufacturer's recommended schedule.</p>
        <p>5.2. All Parties must approve repairs exceeding $500 in cost.</p>
        <p>5.3. Each Party shall promptly report any damage or mechanical issues.</p>
    </div>

    <div class=""section"">
        <h2>6. INSURANCE</h2>
        <p>6.1. The Vehicle shall be fully insured at all times with coverage acceptable to all Parties.</p>
        <p>6.2. All Parties shall be listed as named insured on the policy.</p>
        <p>6.3. Insurance costs shall be shared according to ownership percentages.</p>
    </div>

    <div class=""section"">
        <h2>7. SALE OR TRANSFER</h2>
        <p>7.1. No Party may sell or transfer their ownership interest without first offering to sell to the other Parties.</p>
        <p>7.2. The offering price shall be based on fair market value as determined by an independent appraisal.</p>
        <p>7.3. If no Party wishes to purchase, the selling Party may transfer to a third party with approval from all remaining Parties.</p>
    </div>

    <div class=""section"">
        <h2>8. DISPUTE RESOLUTION</h2>
        <p>8.1. All disputes shall first be attempted to be resolved through good-faith negotiation.</p>
        <p>8.2. If negotiation fails, disputes shall be submitted to mediation before pursuing legal action.</p>
    </div>

    <div class=""section"">
        <h2>9. TERMINATION</h2>
        <p>9.1. This Agreement may be terminated by mutual consent of all Parties.</p>
        <p>9.2. Upon termination, the Vehicle shall be sold and proceeds distributed according to ownership percentages, or one Party may buy out the others.</p>
    </div>

    <div class=""signature-block"">
        <div>
            <p>Signed by all Parties on the date first written above.</p>
        </div>
    </div>
</body>
</html>";

        return new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Vehicle Co-Ownership Agreement",
            Description = "Comprehensive agreement for multiple parties co-owning an electric vehicle",
            Category = DocumentTemplateCategory.Legal,
            TemplateContent = content,
            VariablesJson = JsonSerializer.Serialize(variables),
            IsActive = true,
            CreatedBy = createdBy,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private DocumentTemplate CreateCostSharingAgreementTemplate(Guid createdBy)
    {
        var variables = new List<TemplateVariable>
        {
            new() { Name = "groupName", Label = "Group Name", Type = "text", Required = true },
            new() { Name = "vehicleModel", Label = "Vehicle Model", Type = "text", Required = true },
            new() { Name = "memberNames", Label = "Member Names", Type = "text", Required = true },
            new() { Name = "costCategories", Label = "Cost Categories", Type = "text", Required = true, Placeholder = "Fuel, Maintenance, Insurance" },
            new() { Name = "billingCycle", Label = "Billing Cycle", Type = "text", Required = true, Placeholder = "Monthly" },
            new() { Name = "effectiveDate", Label = "Effective Date", Type = "date", Required = true }
        };

        var content = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; padding: 40px; }
        h1 { text-align: center; color: #2c3e50; }
        h2 { color: #34495e; margin-top: 20px; }
        .section { margin-bottom: 20px; }
    </style>
</head>
<body>
    <h1>COST-SHARING AGREEMENT</h1>
    <p><strong>Effective Date:</strong> {{effectiveDate}}</p>

    <div class=""section"">
        <h2>1. PARTIES</h2>
        <p><strong>Group:</strong> {{groupName}}</p>
        <p><strong>Members:</strong> {{memberNames}}</p>
        <p><strong>Vehicle:</strong> {{vehicleModel}}</p>
    </div>

    <div class=""section"">
        <h2>2. SHARED COSTS</h2>
        <p>The following costs shall be shared equally among all members:</p>
        <p>{{costCategories}}</p>
        <p><strong>Billing Cycle:</strong> {{billingCycle}}</p>
    </div>

    <div class=""section"">
        <h2>3. PAYMENT TERMS</h2>
        <p>3.1. Each member shall pay their proportionate share within 15 days of receiving the billing statement.</p>
        <p>3.2. Late payments may incur a fee of 5% per month.</p>
    </div>

    <div class=""section"">
        <h2>4. COST ALLOCATION</h2>
        <p>4.1. Fixed costs (insurance, registration) shall be divided equally.</p>
        <p>4.2. Variable costs (charging, maintenance) may be allocated based on usage if tracking is available.</p>
    </div>

    <div class=""section"">
        <h2>5. RECORD KEEPING</h2>
        <p>5.1. All expenses shall be documented with receipts.</p>
        <p>5.2. A designated member shall maintain financial records accessible to all.</p>
    </div>
</body>
</html>";

        return new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Cost-Sharing Agreement",
            Description = "Agreement for sharing vehicle-related expenses among group members",
            Category = DocumentTemplateCategory.Financial,
            TemplateContent = content,
            VariablesJson = JsonSerializer.Serialize(variables),
            IsActive = true,
            CreatedBy = createdBy,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private DocumentTemplate CreateUsageTermsTemplate(Guid createdBy)
    {
        var variables = new List<TemplateVariable>
        {
            new() { Name = "groupName", Label = "Group Name", Type = "text", Required = true },
            new() { Name = "vehicleModel", Label = "Vehicle Model", Type = "text", Required = true },
            new() { Name = "maxUsageHoursPerWeek", Label = "Max Usage Hours Per Week", Type = "text", Required = true },
            new() { Name = "bookingLeadTime", Label = "Booking Lead Time (hours)", Type = "text", Required = true },
            new() { Name = "effectiveDate", Label = "Effective Date", Type = "date", Required = true }
        };

        var content = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; padding: 40px; }
        h1 { text-align: center; color: #2c3e50; }
        h2 { color: #34495e; margin-top: 20px; }
    </style>
</head>
<body>
    <h1>USAGE TERMS AND CONDITIONS</h1>
    <p><strong>Group:</strong> {{groupName}}</p>
    <p><strong>Vehicle:</strong> {{vehicleModel}}</p>
    <p><strong>Effective Date:</strong> {{effectiveDate}}</p>

    <h2>1. BOOKING & SCHEDULING</h2>
    <p>1.1. Members must book the vehicle at least {{bookingLeadTime}} hours in advance.</p>
    <p>1.2. Maximum usage is {{maxUsageHoursPerWeek}} hours per week per member to ensure fair access.</p>
    <p>1.3. Cancellations must be made at least 24 hours in advance.</p>

    <h2>2. USAGE GUIDELINES</h2>
    <p>2.1. Vehicle must be returned with at least 20% battery charge.</p>
    <p>2.2. Interior must be kept clean; exterior washing recommended after extended trips.</p>
    <p>2.3. No smoking, vaping, or pets without prior group approval.</p>

    <h2>3. PROHIBITED USES</h2>
    <p>3.1. Commercial ridesharing or delivery services without group consent.</p>
    <p>3.2. Off-road driving or racing.</p>
    <p>3.3. Lending to non-members.</p>

    <h2>4. VIOLATIONS</h2>
    <p>4.1. First violation: Written warning.</p>
    <p>4.2. Second violation: Temporary suspension of usage rights.</p>
    <p>4.3. Third violation: Removal from group or legal action.</p>
</body>
</html>";

        return new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Usage Terms and Conditions",
            Description = "Terms and conditions for vehicle usage by group members",
            Category = DocumentTemplateCategory.Usage,
            TemplateContent = content,
            VariablesJson = JsonSerializer.Serialize(variables),
            IsActive = true,
            CreatedBy = createdBy,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private DocumentTemplate CreateMaintenanceContractTemplate(Guid createdBy)
    {
        var variables = new List<TemplateVariable>
        {
            new() { Name = "groupName", Label = "Group Name", Type = "text", Required = true },
            new() { Name = "vehicleModel", Label = "Vehicle Model", Type = "text", Required = true },
            new() { Name = "maintenanceCoordinator", Label = "Maintenance Coordinator", Type = "text", Required = true },
            new() { Name = "maintenanceSchedule", Label = "Maintenance Schedule", Type = "text", Required = true },
            new() { Name = "effectiveDate", Label = "Effective Date", Type = "date", Required = true }
        };

        var content = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; padding: 40px; }
        h1 { text-align: center; color: #2c3e50; }
        h2 { color: #34495e; }
    </style>
</head>
<body>
    <h1>MAINTENANCE RESPONSIBILITY CONTRACT</h1>
    <p><strong>Group:</strong> {{groupName}}</p>
    <p><strong>Vehicle:</strong> {{vehicleModel}}</p>
    <p><strong>Maintenance Coordinator:</strong> {{maintenanceCoordinator}}</p>
    <p><strong>Effective Date:</strong> {{effectiveDate}}</p>

    <h2>1. MAINTENANCE SCHEDULE</h2>
    <p>{{maintenanceSchedule}}</p>

    <h2>2. COORDINATOR RESPONSIBILITIES</h2>
    <p>2.1. Schedule and coordinate all routine maintenance.</p>
    <p>2.2. Maintain records of all service performed.</p>
    <p>2.3. Notify members of upcoming maintenance needs.</p>

    <h2>3. MEMBER RESPONSIBILITIES</h2>
    <p>3.1. Report any issues immediately.</p>
    <p>3.2. Bring vehicle to scheduled maintenance appointments.</p>
    <p>3.3. Pay proportionate share of maintenance costs.</p>

    <h2>4. EMERGENCY REPAIRS</h2>
    <p>4.1. Members may authorize emergency repairs up to $500 without group approval.</p>
    <p>4.2. Repairs exceeding $500 require majority approval.</p>
</body>
</html>";

        return new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Maintenance Responsibility Contract",
            Description = "Contract defining maintenance responsibilities and procedures",
            Category = DocumentTemplateCategory.Maintenance,
            TemplateContent = content,
            VariablesJson = JsonSerializer.Serialize(variables),
            IsActive = true,
            CreatedBy = createdBy,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private DocumentTemplate CreateVehicleSaleAgreementTemplate(Guid createdBy)
    {
        var variables = new List<TemplateVariable>
        {
            new() { Name = "sellerName", Label = "Seller Name", Type = "text", Required = true },
            new() { Name = "buyerName", Label = "Buyer Name", Type = "text", Required = true },
            new() { Name = "vehicleModel", Label = "Vehicle Model", Type = "text", Required = true },
            new() { Name = "vin", Label = "VIN", Type = "text", Required = true },
            new() { Name = "salePrice", Label = "Sale Price", Type = "text", Required = true },
            new() { Name = "saleDate", Label = "Sale Date", Type = "date", Required = true },
            new() { Name = "mileage", Label = "Current Mileage", Type = "text", Required = true }
        };

        var content = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body { font-family: Arial, sans-serif; line-height: 1.6; padding: 40px; }
        h1 { text-align: center; color: #2c3e50; }
        h2 { color: #34495e; }
    </style>
</head>
<body>
    <h1>VEHICLE SALE AGREEMENT</h1>
    <p><strong>Date:</strong> {{saleDate}}</p>

    <h2>SELLER</h2>
    <p>{{sellerName}}</p>

    <h2>BUYER</h2>
    <p>{{buyerName}}</p>

    <h2>VEHICLE DETAILS</h2>
    <p><strong>Make/Model:</strong> {{vehicleModel}}</p>
    <p><strong>VIN:</strong> {{vin}}</p>
    <p><strong>Mileage:</strong> {{mileage}}</p>

    <h2>TERMS OF SALE</h2>
    <p><strong>Purchase Price:</strong> {{salePrice}}</p>
    <p>The Seller agrees to sell and the Buyer agrees to purchase the above-described vehicle for the stated price.</p>

    <h2>VEHICLE CONDITION</h2>
    <p>The vehicle is sold ""as-is"" in its current condition. The Buyer has inspected the vehicle and accepts it in its present state.</p>

    <h2>TRANSFER OF OWNERSHIP</h2>
    <p>Upon receipt of payment, the Seller shall transfer title and provide all necessary documentation to the Buyer.</p>

    <h2>WARRANTIES</h2>
    <p>The Seller warrants that they have clear title to the vehicle and the right to sell it.</p>
</body>
</html>";

        return new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Vehicle Sale Agreement",
            Description = "Agreement for the sale of a vehicle between group members or to external parties",
            Category = DocumentTemplateCategory.Sale,
            TemplateContent = content,
            VariablesJson = JsonSerializer.Serialize(variables),
            IsActive = true,
            CreatedBy = createdBy,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private List<string> ExtractVariablesFromTemplate(string templateContent)
    {
        var regex = new Regex(@"\{\{(\w+)\}\}");
        var matches = regex.Matches(templateContent);
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    private string SubstituteVariables(string templateContent, Dictionary<string, string> variableValues)
    {
        var result = templateContent;
        foreach (var kvp in variableValues)
        {
            var placeholder = $"{{{{{kvp.Key}}}}}";
            result = result.Replace(placeholder, kvp.Value);
        }
        return result;
    }

    private async Task<byte[]> GeneratePdfFromHtmlAsync(string htmlContent)
    {
        // For production, you should use a PDF generation library
        // Options: PuppeteerSharp, IronPdf, SelectPdf, or external service like wkhtmltopdf

        // Using PuppeteerSharp (headless Chrome) - requires installation
        try
        {
            await new BrowserFetcher().DownloadAsync();
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "1cm",
                    Right = "1cm",
                    Bottom = "1cm",
                    Left = "1cm"
                }
            });

            await browser.CloseAsync();
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PuppeteerSharp PDF generation failed");

            // Fallback: Simple HTML to byte conversion (not a real PDF, just for demo)
            // In production, you MUST use a proper PDF library
            throw new InvalidOperationException("PDF generation is not configured. Please install PuppeteerSharp or another PDF library.", ex);
        }
    }

    private async Task<string> ComputeFileHashAsync(Stream stream)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        stream.Position = 0;
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private TemplateListResponse MapToListResponse(DocumentTemplate template)
    {
        var variables = JsonSerializer.Deserialize<List<TemplateVariable>>(template.VariablesJson) ?? new List<TemplateVariable>();
        var usageCount = _context.Documents.Count(d => d.TemplateId == template.Id);

        return new TemplateListResponse
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            CategoryName = template.Category.ToString(),
            VariableCount = variables.Count,
            Variables = variables,
            IsActive = template.IsActive,
            Version = template.Version,
            PreviewImageUrl = template.PreviewImageUrl,
            CreatedAt = template.CreatedAt,
            CreatedByName = $"{template.Creator.FirstName} {template.Creator.LastName}",
            UsageCount = usageCount
        };
    }

    private async Task<TemplateDetailResponse> MapToDetailResponse(DocumentTemplate template)
    {
        var variables = JsonSerializer.Deserialize<List<TemplateVariable>>(template.VariablesJson) ?? new List<TemplateVariable>();
        var usageCount = await _context.Documents.CountAsync(d => d.TemplateId == template.Id);

        return new TemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            TemplateContent = template.TemplateContent,
            Variables = variables,
            IsActive = template.IsActive,
            Version = template.Version,
            PreviewImageUrl = template.PreviewImageUrl,
            CreatedAt = template.CreatedAt,
            CreatedBy = template.CreatedBy,
            CreatedByName = $"{template.Creator.FirstName} {template.Creator.LastName}",
            UsageCount = usageCount
        };
    }

    #endregion
}
