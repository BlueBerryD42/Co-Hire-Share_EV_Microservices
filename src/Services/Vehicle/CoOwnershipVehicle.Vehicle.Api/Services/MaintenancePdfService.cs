using CoOwnershipVehicle.Vehicle.Api.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

public class MaintenancePdfService : IMaintenancePdfService
{
    private readonly VehicleDbContext _context;
    private readonly ILogger<MaintenancePdfService> _logger;

    public MaintenancePdfService(VehicleDbContext context, ILogger<MaintenancePdfService> logger)
    {
        _context = context;
        _logger = logger;

        // Required for QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateMaintenanceReportPdfAsync(Guid maintenanceRecordId)
    {
        var record = await _context.MaintenanceRecords
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == maintenanceRecordId);

        if (record == null)
        {
            throw new InvalidOperationException($"Maintenance record {maintenanceRecordId} not found");
        }

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text("Maintenance Completion Report")
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(20);

                        // Vehicle Information
                        x.Item().Element(container => RenderSection(container, "Vehicle Information", c =>
                        {
                            c.Column(col =>
                            {
                                col.Item().Text($"Model: {record.Vehicle?.Model ?? "N/A"}");
                                col.Item().Text($"Plate Number: {record.Vehicle?.PlateNumber ?? "N/A"}");
                                col.Item().Text($"Odometer Reading: {record.OdometerReading:N0} km");
                            });
                        }));

                        // Service Information
                        x.Item().Element(container => RenderSection(container, "Service Information", c =>
                        {
                            c.Column(col =>
                            {
                                col.Item().Text($"Service Type: {record.ServiceType}");
                                col.Item().Text($"Service Date: {record.ScheduledDate:yyyy-MM-dd HH:mm}");
                                col.Item().Text($"Service Provider: {record.ServiceProvider}");
                                col.Item().Text($"Completion: {record.CompletionPercentage}%");
                            });
                        }));

                        // Work Performed
                        x.Item().Element(container => RenderSection(container, "Work Performed", c =>
                        {
                            c.Text(record.WorkPerformed);
                        }));

                        // Parts Replaced
                        if (!string.IsNullOrEmpty(record.PartsReplaced))
                        {
                            x.Item().Element(container => RenderSection(container, "Parts Replaced", c =>
                            {
                                c.Text(record.PartsReplaced);
                            }));
                        }

                        // Cost Information
                        x.Item().Element(container => RenderSection(container, "Cost Information", c =>
                        {
                            c.Column(col =>
                            {
                                col.Item().Text($"Total Cost: ${record.ActualCost:F2}").SemiBold().FontSize(14);
                            });
                        }));

                        // Next Service
                        if (record.NextServiceDue.HasValue || record.NextServiceOdometer.HasValue)
                        {
                            x.Item().Element(container => RenderSection(container, "Next Service Due", c =>
                            {
                                c.Column(col =>
                                {
                                    if (record.NextServiceDue.HasValue)
                                        col.Item().Text($"Date: {record.NextServiceDue.Value:yyyy-MM-dd}");
                                    if (record.NextServiceOdometer.HasValue)
                                        col.Item().Text($"Odometer: {record.NextServiceOdometer.Value:N0} km");
                                });
                            }));
                        }

                        // Service Provider Rating
                        if (record.ServiceProviderRating.HasValue)
                        {
                            x.Item().Element(container => RenderSection(container, "Service Provider Rating", c =>
                            {
                                c.Column(col =>
                                {
                                    col.Item().Text($"Rating: {new string('★', record.ServiceProviderRating.Value)}{new string('☆', 5 - record.ServiceProviderRating.Value)}");
                                    if (!string.IsNullOrEmpty(record.ServiceProviderReview))
                                        col.Item().Text($"Review: {record.ServiceProviderReview}");
                                });
                            }));
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Generated on ");
                        x.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).SemiBold();
                        x.Span(" | Co-Ownership Vehicle System");
                    });
            });
        }).GeneratePdf();

        _logger.LogInformation("Generated PDF report for maintenance record {RecordId}, size: {Size} bytes",
            maintenanceRecordId, pdfBytes.Length);

        return pdfBytes;
    }

    private static void RenderSection(IContainer container, string title, Action<IContainer> content)
    {
        container.Column(column =>
        {
            column.Item().Text(title).SemiBold().FontSize(14).FontColor(Colors.Blue.Darken2);
            column.Item().PaddingTop(5).PaddingLeft(10).Element(content);
        });
    }
}
