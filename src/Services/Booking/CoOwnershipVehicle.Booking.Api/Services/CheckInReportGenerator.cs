using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CheckInType = CoOwnershipVehicle.Domain.Entities.CheckInType;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class CheckInReportGenerator : ICheckInReportGenerator
{
    private static bool _licenseApplied;

    public CheckInReportGenerator()
    {
        if (!_licenseApplied)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            _licenseApplied = true;
        }
    }

    public Task<byte[]> GenerateAsync(BookingCheckInHistoryDto history, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);

                page.Header().Text(text =>
                {
                    text.Span("Vehicle Check-In Report").FontSize(20).Bold();
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text($"Booking ID: {history.BookingId}");
                    column.Item().Text($"Vehicle: {history.VehicleDisplayName}");
                    column.Item().Text($"Booking Owner: {history.BookingOwnerName}");
                    column.Item().Text($"Planned window: {history.TripStatistics.PlannedStart:g} - {history.TripStatistics.PlannedEnd:g} (UTC)");
                    column.Item().Element(container => BuildTripStatistics(container, history.TripStatistics));

                    foreach (var record in history.Records)
                    {
                        column.Item().Element(container => BuildRecordSection(container, record));
                    }
                });

                page.Footer().AlignCenter().Text($"Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            });
        });

        var pdf = document.GeneratePdf();
        return Task.FromResult(pdf);
    }

    private static void BuildTripStatistics(IContainer container, CheckInTripStatisticsDto stats)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(170);
                columns.RelativeColumn();
            });

            void AddRow(string label, string? value)
            {
                table.Cell().Element(CellLabel).Text(label).SemiBold();
                table.Cell().Element(CellValue).Text(value ?? "-");
            }

            AddRow("Planned duration (min)", stats.PlannedDurationMinutes.ToString("F1", CultureInfo.InvariantCulture));
            AddRow("Actual duration (min)", stats.ActualDurationMinutes?.ToString("F1", CultureInfo.InvariantCulture));
            AddRow("Distance travelled (km)", stats.TripDistance?.ToString(CultureInfo.InvariantCulture));
            AddRow("Average speed (km/h)", stats.AverageSpeedKph?.ToString("F1", CultureInfo.InvariantCulture));
            AddRow("Late return (min)", stats.LateReturnMinutes?.ToString("F1", CultureInfo.InvariantCulture));
            AddRow("Late fee amount", stats.LateFeeAmount.HasValue ? stats.LateFeeAmount.Value.ToString("C", CultureInfo.InvariantCulture) : null);
        });
    }

    private static void BuildRecordSection(IContainer container, CheckInRecordDetailDto record)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(8).Column(column =>
        {
            column.Spacing(6);
            column.Item().Text(record.Record.Type == CheckInType.CheckOut ? "Check-out" : "Check-in")
                .FontSize(16)
                .Bold();
            column.Item().Text($"Timestamp: {record.Record.CheckInTime:g} UTC");
            column.Item().Text($"Odometer: {record.Record.Odometer}");

            if (!string.IsNullOrWhiteSpace(record.Record.Notes))
            {
                column.Item().Text($"Notes: {record.Record.Notes}");
            }

            if (record.DamageReports.Count > 0)
            {
                column.Item().Text("Damage reports:").Bold();
                column.Item().Column(listColumn =>
                {
                    foreach (var damage in record.DamageReports)
                    {
                        listColumn.Item().Text($"{damage.CreatedAt:g} - {damage.Description} ({damage.Severity})");
                    }
                });
            }

            if (record.Record.Photos.Count > 0)
            {
                column.Item().Text($"Photos captured: {record.Record.Photos.Count}");
            }

            if (record.Record.IsLateReturn)
            {
                var minutesLate = record.Record.LateReturnMinutes.HasValue
                    ? $"{record.Record.LateReturnMinutes.Value:F0} minutes"
                    : "Yes";
                var feeText = record.Record.LateReturnFee != null
                    ? $" Late fee: {record.Record.LateReturnFee.FeeAmount.ToString("C", CultureInfo.InvariantCulture)} ({record.Record.LateReturnFee.Status})."
                    : string.Empty;

                column.Item().Text($"Late return: {minutesLate}.{feeText}").FontColor(Colors.Red.Medium);
            }
        });
    }

    private static IContainer CellLabel(IContainer container) =>
        container.PaddingVertical(2);

    private static IContainer CellValue(IContainer container) =>
        container.PaddingVertical(2);
}
