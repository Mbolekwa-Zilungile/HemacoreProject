
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HemaCore.Services
{
    public class VerifiedResultRow
    {
        public string TestName { get; set; } = "";
        public string ResultValue { get; set; } = "";
        public string Unit { get; set; } = "";
        public string NormalRange { get; set; } = "";
        public string Notes { get; set; } = "";
        public bool IsAbnormal { get; set; }
        public DateTime? DateCaptured { get; set; }
    }

    public class VerifiedResultsPdfService
    {
        public byte[] Generate(
            int requestId,
            string patientName,
            string doctorName,
            DateTime requestDate,
            string urgency,
            List<VerifiedResultRow> results)
        {
            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(column =>
                    {
                        column.Item()
                            .Text("HemaCore")
                            .FontSize(22)
                            .Bold()
                            .FontColor(Colors.Teal.Darken2);

                        column.Item()
                            .Text("Verified Laboratory Results")
                            .FontSize(16)
                            .Bold();

                        column.Item()
                            .PaddingTop(5)
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().PaddingVertical(15).Column(column =>
                    {
                        column.Spacing(8);

                        column.Item().Text($"Request number: {requestId}");
                        column.Item().Text($"Patient: {patientName}");
                        column.Item().Text($"Requesting doctor: {doctorName}");
                        column.Item().Text(
                            $"Request date: {requestDate:dd MMM yyyy}");
                        column.Item().Text($"Urgency: {urgency}");

                        column.Item().PaddingTop(12)
                            .Text("Test Results")
                            .FontSize(13)
                            .Bold();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "Test");
                                HeaderCell(header, "Result");
                                HeaderCell(header, "Unit");
                                HeaderCell(header, "Normal range");
                            });

                            foreach (var result in results)
                            {
                                BodyCell(table, result.TestName);

                                var resultCell = table.Cell()
                                    .Element(CellStyle);

                                if (result.IsAbnormal)
                                {
                                    resultCell.Text(
                                        $"{result.ResultValue} (Abnormal)")
                                        .FontColor(Colors.Red.Darken2)
                                        .Bold();
                                }
                                else
                                {
                                    resultCell.Text(result.ResultValue);
                                }

                                BodyCell(table, result.Unit);
                                BodyCell(table, result.NormalRange);

                                if (!string.IsNullOrWhiteSpace(result.Notes))
                                {
                                    table.Cell()
                                        .ColumnSpan(4)
                                        .Element(CellStyle)
                                        .Text($"Notes: {result.Notes}");
                                }
                            }
                        });

                        column.Item().PaddingTop(12).Text(
                            "This report contains laboratory results verified " +
                            "by the laboratory. The requesting doctor must " +
                            "review the results and release them to the " +
                            "patient through HemaCore when appropriate.");

                        column.Item().PaddingTop(8).Text(
                            "Confidential medical information.")
                            .Italic();
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("HemaCore | Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        private static void HeaderCell(
            TableCellDescriptor header,
            string text)
        {
            header.Cell()
                .Element(CellStyle)
                .Background(Colors.Grey.Lighten3)
                .Text(text)
                .Bold();
        }

        private static void BodyCell(
            TableDescriptor table,
            string text)
        {
            table.Cell()
                .Element(CellStyle)
                .Text(string.IsNullOrWhiteSpace(text) ? "—" : text);
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(6)
                .PaddingHorizontal(4);
        }
    }
}
