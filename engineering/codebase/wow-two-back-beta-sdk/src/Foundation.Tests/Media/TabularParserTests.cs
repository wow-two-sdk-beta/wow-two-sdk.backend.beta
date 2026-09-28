using System.Text;
using AwesomeAssertions;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Csv;
using WoW.Two.Sdk.Backend.Beta.Media.Excel;
using WoW.Two.Sdk.Backend.Beta.Media.Excel.Exporters;
using WoW.Two.Sdk.Backend.Beta.Media.Excel.Parsers;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Parsers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media;

/// <summary>Spreadsheet imports: XLSX round trips, forgiving headers, row-precise errors and format selection.</summary>
public sealed class TabularParserTests
{
    private static readonly ServiceProvider Services = new ServiceCollection().AddCsvExport().AddExcelExport().BuildServiceProvider();

    [Fact]
    public async Task Xlsx_ShouldReadBackWhatTheExporterWrote()
    {
        Product[] rows =
        [
            new() { Id = Guid.NewGuid(), Name = "Lamp", Price = 19.99m, Stock = 3, Active = true, Kind = ProductKind.Light, AddedOn = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.NewGuid(), Name = "Desk", Price = 250m, Stock = null, Active = false, Kind = ProductKind.Furniture, AddedOn = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc) },
        ];
        using var workbook = new MemoryStream();
        await Services.GetRequiredService<ExcelTabularExporter>().WriteAsync(rows, workbook);
        workbook.Position = 0;

        var read = await Services.GetRequiredService<IExcelParser>().ReadAsync<Product>(workbook).ToListAsync();

        read.Should().BeEquivalentTo(rows);
    }

    [Fact]
    public async Task Xlsx_ShouldMatchForgivingHeaders_AndSkipBlankRowsAndUnknownColumns()
    {
        var sheet = Workbook(
            ["Full Name", "unit_price", "IS-ACTIVE", "Notes"],
            ["Aziza", "12.5", "yes", "ignored"],
            [null, null, null, null],
            ["Bobur", "7", "no", null]);

        var read = await Services.GetRequiredService<IExcelParser>().ReadAsync<Customer>(sheet).ToListAsync();

        read.Should().BeEquivalentTo([new Customer { FullName = "Aziza", UnitPrice = 12.5m, IsActive = true }, new Customer { FullName = "Bobur", UnitPrice = 7m, IsActive = false }]);
    }

    [Fact]
    public async Task Xlsx_ShouldNameTheRowAndColumnOfABadCell()
    {
        var sheet = Workbook(["Full Name", "Unit Price"], ["Aziza", "12.5"], ["Bobur", "seven"]);

        var read = async () => await Services.GetRequiredService<IExcelParser>().ReadAsync<Customer>(sheet).ToListAsync();

        var error = (await read.Should().ThrowAsync<TabularRowException>()).Which;
        (error.Row, error.Column).Should().Be((3, "Unit Price"));
        Services.GetServices<IExceptionMappingRule>().Select(rule => rule.TryMap(error)).First(mapped => mapped is not null)!.Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public async Task Parsers_ShouldBeSelectableByFormat()
    {
        var parsers = Services.GetServices<ITabularParser>().ToDictionary(parser => parser.Format);
        parsers.Keys.Should().BeEquivalentTo([TabularFormat.Csv, TabularFormat.Xlsx]);

        var csv = new MemoryStream(Encoding.UTF8.GetBytes("FullName,UnitPrice,IsActive\nAziza,12.5,true\n"));
        (await parsers[TabularFormat.Csv].ReadAsync<Customer>(csv).ToListAsync()).Single().FullName.Should().Be("Aziza");
    }

    private static MemoryStream Workbook(params string?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Import");
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] is { } value)
                    sheet.Cell(row + 1, column + 1).Value = value;
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    public enum ProductKind
    {
        Light,
        Furniture,
    }

    public sealed class Product
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int? Stock { get; set; }

        public bool Active { get; set; }

        public ProductKind Kind { get; set; }

        public DateTime AddedOn { get; set; }
    }

    public sealed class Customer
    {
        public string FullName { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public bool IsActive { get; set; }
    }
}
