using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Ordering.Application;

internal static class OrderExcelExporter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static string FileName(AdminOrdersQuery query, DateTimeOffset exportedAt)
    {
        var period = (query.From, query.To) switch
        {
            ({ } from, { } to) => FormattableString.Invariant($"Tu-{from:yyyy-MM-dd}_Den-{to:yyyy-MM-dd}"),
            ({ } from, null) => FormattableString.Invariant($"Tu-{from:yyyy-MM-dd}"),
            (null, { } to) => FormattableString.Invariant($"Den-{to:yyyy-MM-dd}"),
            _ => "Tat-ca-thoi-gian",
        };
        var vietnamTime = exportedAt.ToOffset(TimeSpan.FromHours(7));
        return FormattableString.Invariant(
            $"Sach-Luat-Viet-Nam_Bao-cao-don-hang_{period}_Xuat-{vietnamTime:yyyy-MM-dd_HH-mm-ss}.xlsx");
    }

    public static byte[] Export(
        IReadOnlyList<Order> orders,
        AdminOrdersQuery query,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
            WriteDocument(archive, "[Content_Types].xml", new XElement(content + "Types",
                new XElement(content + "Default", new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(content + "Default", new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                ContentType("/xl/workbook.xml", "sheet.main"),
                ContentType("/xl/styles.xml", "styles"),
                Enumerable.Range(1, 3).Select(index =>
                    ContentType($"/xl/worksheets/sheet{index}.xml", "worksheet"))));

            XNamespace relationship = PackageRelationshipNamespace;
            WriteDocument(archive, "_rels/.rels", new XElement(relationship + "Relationships",
                Relationship("rId1", "officeDocument", "xl/workbook.xml")));
            WriteDocument(archive, "xl/_rels/workbook.xml.rels", new XElement(relationship + "Relationships",
                Relationship("rId4", "styles", "styles.xml"),
                Enumerable.Range(1, 3).Select(index =>
                    Relationship($"rId{index}", "worksheet", $"worksheets/sheet{index}.xml"))));
            WriteDocument(archive, "xl/styles.xml", XElement.Parse(Styles));

            XNamespace spreadsheet = SpreadsheetNamespace;
            XNamespace reference = RelationshipNamespace;
            var names = new[] { "Thống kê", "Đơn hàng", "Chi tiết sách" };
            WriteDocument(archive, "xl/workbook.xml", new XElement(spreadsheet + "workbook",
                new XElement(spreadsheet + "sheets", names.Select((name, index) =>
                    new XElement(spreadsheet + "sheet", new XAttribute("name", name),
                        new XAttribute("sheetId", index + 1), new XAttribute(reference + "id", $"rId{index + 1}"))))));

            WriteSheet(archive, 1, StatisticsRows(orders, query), cancellationToken);
            WriteSheet(archive, 2, OrderRows(orders), cancellationToken);
            WriteSheet(archive, 3, ItemRows(orders), cancellationToken);
        }

        return stream.ToArray();
    }

    private static XElement ContentType(string partName, string type)
    {
        XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
        return new XElement(content + "Override", new XAttribute("PartName", partName),
            new XAttribute("ContentType", $"application/vnd.openxmlformats-officedocument.spreadsheetml.{type}+xml"));
    }

    private static XElement Relationship(string id, string type, string target)
    {
        XNamespace relationship = PackageRelationshipNamespace;
        return new XElement(relationship + "Relationship", new XAttribute("Id", id),
            new XAttribute("Type", $"{RelationshipNamespace}/{type}"), new XAttribute("Target", target));
    }

    private static void WriteDocument(ZipArchive archive, string path, XElement root)
    {
        using var entry = archive.CreateEntry(path).Open();
        new XDocument(root).Save(entry);
    }

    private static IEnumerable<object?[]> StatisticsRows(IReadOnlyList<Order> orders, AdminOrdersQuery query)
    {
        var sold = orders.Where(order =>
            order.Status is OrderStatus.Delivered or OrderStatus.Completed).ToList();
        yield return ["Chỉ tiêu", "Giá trị"];
        yield return ["Tổng số đơn theo bộ lọc", orders.Count];
        yield return ["Doanh thu sách (VND)", sold.Sum(order => order.Subtotal - order.Discount)];
        yield return ["Số sách đã bán (cuốn)", sold.Sum(order => order.Items.Sum(item => (long)item.Quantity))];
        yield return ["Cách tính", "Chỉ đơn delivered/completed; tiền sách sau giảm giá, không gồm phí ship"];
        yield return ["Ngày tạo từ (giờ Việt Nam)", query.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)];
        yield return ["Ngày tạo đến (giờ Việt Nam)", query.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)];
        yield return ["Trạng thái lọc", query.Status];
        yield return ["Phương thức lọc", query.PaymentMethod];
        yield return ["Tìm kiếm", query.Q];
    }

    private static IEnumerable<object?[]> OrderRows(IReadOnlyList<Order> orders)
    {
        yield return ["Mã đơn", "Ngày tạo (giờ Việt Nam)", "Trạng thái", "Phương thức", "Thanh toán",
            "Khách hàng", "Số điện thoại", "Email", "Tỉnh/thành", "Phường/xã", "Địa chỉ", "Số sách",
            "Tạm tính", "Giảm giá", "Phí ship", "Giảm phí ship", "Tổng thanh toán"];
        foreach (var order in orders)
        {
            yield return [order.OrderCode, VietnamTime(order.CreatedAt),
                SnakeCaseEnumConverter<OrderStatus>.ToProvider(order.Status),
                SnakeCaseEnumConverter<PaymentMethod>.ToProvider(order.PaymentMethod),
                SnakeCaseEnumConverter<PaymentStatus>.ToProvider(order.PaymentStatus),
                order.CustomerName, order.Phone, order.Email, order.ShippingAddress.Province,
                order.ShippingAddress.Ward, order.ShippingAddress.StreetAddress,
                order.Items.Sum(item => (long)item.Quantity), order.Subtotal, order.Discount,
                order.ShippingFee, order.ShippingDiscount, order.Total];
        }
    }

    private static IEnumerable<object?[]> ItemRows(IReadOnlyList<Order> orders)
    {
        yield return ["Mã đơn", "SKU", "Tên sách", "Ấn bản", "ISBN", "Loại bìa", "Đơn giá", "Số lượng",
            "Thành tiền", "Giảm giá", "Tiền sách sau giảm"];
        foreach (var order in orders)
        {
            foreach (var item in order.Items.OrderBy(item => item.Id))
            {
                yield return [order.OrderCode, item.SkuCode, item.BookTitle, item.EditionName, item.Isbn,
                    item.CoverType, item.UnitPrice, item.Quantity, item.LineTotal,
                    item.DiscountAmount, item.LineTotal - item.DiscountAmount];
            }
        }
    }

    private static string VietnamTime(DateTime value) =>
        value.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private const string Styles = """
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="1"><numFmt numFmtId="164" formatCode="#,##0 &quot;₫&quot;"/></numFmts>
          <fonts count="3">
            <font><sz val="11"/><color rgb="FF243746"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="12"/><color rgb="FF00695C"/><name val="Calibri"/></font>
          </fonts>
          <fills count="5">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF00695C"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFF0F7F6"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFE0F2F1"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border><left/><right/><top/><bottom style="hair"><color rgb="FFD6E5E2"/></bottom><diagonal/></border>
          </borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="11">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyAlignment="1">
              <alignment vertical="center" wrapText="1" indent="1"/>
            </xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyAlignment="1">
              <alignment vertical="center" wrapText="1" indent="1"/>
            </xf>
            <xf numFmtId="0" fontId="0" fillId="3" borderId="1" xfId="0" applyAlignment="1">
              <alignment vertical="center" wrapText="1" indent="1"/>
            </xf>
            <xf numFmtId="3" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center" indent="1"/>
            </xf>
            <xf numFmtId="3" fontId="0" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center" indent="1"/>
            </xf>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center" indent="1"/>
            </xf>
            <xf numFmtId="164" fontId="0" fillId="3" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center" indent="1"/>
            </xf>
            <xf numFmtId="0" fontId="2" fillId="4" borderId="1" xfId="0" applyAlignment="1">
              <alignment vertical="center" wrapText="1" indent="1"/>
            </xf>
            <xf numFmtId="3" fontId="2" fillId="4" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center" indent="1"/>
            </xf>
            <xf numFmtId="164" fontId="2" fillId="4" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center" indent="1"/>
            </xf>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;

    private static int[] ColumnWidths(int sheet) => sheet switch
    {
        1 => [38, 92],
        2 => [24, 24, 24, 22, 20, 30, 19, 38, 26, 28, 50, 13, 22, 22, 20, 22, 25],
        _ => [24, 18, 66, 32, 21, 22, 22, 13, 24, 22, 27],
    };

    private static string ColumnName(int column)
    {
        var name = string.Empty;
        for (var value = column; value > 0; value = (value - 1) / 26)
        {
            name = (char)('A' + (value - 1) % 26) + name;
        }

        return name;
    }

    private static int CellStyle(int sheet, int row, int column, object? value)
    {
        if (row == 1)
        {
            return 1;
        }

        if (sheet == 1 && row is >= 2 and <= 4)
        {
            return column == 1 ? 8 : row == 3 ? 10 : 9;
        }

        var band = row % 2;
        return value switch
        {
            decimal => 6 + band,
            int or long => 4 + band,
            _ => 2 + band,
        };
    }

    private static void WriteSheet(
        ZipArchive archive,
        int index,
        IEnumerable<object?[]> rows,
        CancellationToken cancellationToken)
    {
        using var entry = archive.CreateEntry($"xl/worksheets/sheet{index}.xml").Open();
        using var writer = XmlWriter.Create(entry, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetPr", SpreadsheetNamespace);
        writer.WriteStartElement("tabColor", SpreadsheetNamespace);
        writer.WriteAttributeString("rgb", "FF00695C");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace);
        writer.WriteAttributeString("showGridLines", "0");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", SpreadsheetNamespace);
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        var widths = ColumnWidths(index);
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var column = 0; column < widths.Length; column++)
        {
            writer.WriteStartElement("col", SpreadsheetNamespace);
            var position = (column + 1).ToString(CultureInfo.InvariantCulture);
            writer.WriteAttributeString("min", position);
            writer.WriteAttributeString("max", position);
            writer.WriteAttributeString("width", widths[column].ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        var rowNumber = 0;
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            writer.WriteStartElement("row", SpreadsheetNamespace);
            writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            var lines = row.Select((value, column) => (value?.ToString() ?? string.Empty).Split('\n')
                .Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length * 1.15 / (widths[column] - 3))))).Max();
            var height = rowNumber == 1 ? 36 : Math.Clamp(lines * 16 + 14, 34, 300);
            writer.WriteAttributeString("ht", height.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customHeight", "1");
            for (var column = 0; column < row.Length; column++)
            {
                var value = row[column];
                writer.WriteStartElement("c", SpreadsheetNamespace);
                writer.WriteAttributeString("r", $"{ColumnName(column + 1)}{rowNumber}");
                writer.WriteAttributeString("s", CellStyle(index, rowNumber, column + 1, value)
                    .ToString(CultureInfo.InvariantCulture));
                if (value is int or long or decimal)
                {
                    writer.WriteElementString("v", SpreadsheetNamespace,
                        Convert.ToString(value, CultureInfo.InvariantCulture));
                }
                else
                {
                    // Inline strings preserve phone/ISBN zeros and never execute user input as Excel formulas.
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", SpreadsheetNamespace);
                    writer.WriteStartElement("t", SpreadsheetNamespace);
                    writer.WriteAttributeString("xml", "space", null, "preserve");
                    writer.WriteString(value?.ToString() ?? string.Empty);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        if (index != 1)
        {
            writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
            writer.WriteAttributeString("ref", $"A1:{ColumnName(widths.Length)}{rowNumber}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }
}
