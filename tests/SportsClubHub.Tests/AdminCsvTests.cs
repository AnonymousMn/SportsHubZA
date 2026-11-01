using SportsClubHub.Web.Services;

namespace SportsClubHub.Tests;

public class AdminCsvTests
{
    [Theory]
    [InlineData("Netball", "Netball")]
    [InlineData("Rugby, Union", "\"Rugby, Union\"")]
    [InlineData("Say \"hi\"", "\"Say \"\"hi\"\"\"")]
    [InlineData(null, "")]
    public void Cell_QuotesOnlyWhenNeeded(string? input, string expected) =>
        Assert.Equal(expected, AdminCsv.Cell(input));

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil.test\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    [InlineData("  =HYPERLINK(\"http://evil.test\")")]
    public void Cell_NeutralisesSpreadsheetFormulas(string input) =>
        Assert.StartsWith("'", AdminCsv.Cell(input).TrimStart('"'));

    [Fact]
    public void Build_WritesHeaderAndRowsWithWindowsLineEndings()
    {
        var csv = AdminCsv.Build(new[] { "Sport", "Interest" }, new[] { (IReadOnlyList<string>)new[] { "Chess", "3" } });
        Assert.Equal("Sport,Interest\r\nChess,3\r\n", csv);
    }
}
