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
                Enumerable.Range(1, 3).Select(index =>
                    ContentType($"/xl/worksheets/sheet{index}.xml", "worksheet"))));

            XNamespace relationship = PackageRelationshipNamespace;
            WriteDocument(archive, "_rels/.rels", new XElement(relationship + "Relationships",
                Relationship("rId1", "officeDocument", "xl/workbook.xml")));
            WriteDocument(archive, "xl/_rels/workbook.xml.rels", new XElement(relationship + "Relationships",
                Enumerable.Range(1, 3).Select(index =>
                    Relationship($"rId{index}", "worksheet", $"worksheets/sheet{index}.xml"))));

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
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteStartElement("row", SpreadsheetNamespace);
            foreach (var value in row)
            {
                writer.WriteStartElement("c", SpreadsheetNamespace);
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
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }
}
