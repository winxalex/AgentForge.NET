
using ClosedXML.Excel;

using System.Dynamic;

namespace Chat2Report.Services
{
    public interface IExcelService
    {
        XLWorkbook GenerateExcelReport(List<dynamic> data, string worksheetName, string header, Dictionary<string, object> styleProperties);
        XLWorkbook GenerateExcelReport(List<dynamic> data, string worksheetName);
        XLWorkbook GenerateTreeViewExcelReport(List<dynamic> data, string worksheetName, string header, Dictionary<string, object> headerStyles = null, Dictionary<string, object> groupTitleStyles = null, Dictionary<string, object> cellStyles = null);
    }

    public class ExcelService : IExcelService
    {

        public XLWorkbook GenerateTreeViewExcelReport(List<dynamic> data, string worksheetName, string header, Dictionary<string, object> headerStyles = null, Dictionary<string, object> groupTitleStyles = null, Dictionary<string, object> cellStyles = null)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(worksheetName);

            // Style the header row
            worksheet.Row(1).InsertRowsAbove(2);

            ClosedXML.Excel.IXLCell iXLCell = worksheet.Cell(1, 1);

            iXLCell.Value = header;
            IXLStyle headerStyle = iXLCell.Style;

            // Apply the header style properties (if provided)
            if (headerStyles != null)
            {
                ApplyStyles(headerStyle, headerStyles);
            }

            // Add data
            int rowIndex = 3;

            foreach (var item in data)
            {
                int columnIndex = 1;
                bool isGroupTitle = false;
                int currentIdentitySize = 0;
                int firstDataColumnIndex = 0;

                foreach (var property in (IDictionary<string, object>)item)
                {
                    string value = property.Value as string;
                    if (property.Key.StartsWith("Column"))
                    {
                        currentIdentitySize++;
                        if (!string.IsNullOrEmpty(value))
                        {
                            worksheet.Cell(rowIndex, columnIndex).Value = value;
                            // Apply cell styles for regular cells
                            if (!isGroupTitle && cellStyles != null)
                                ApplyStyles(worksheet.Cell(rowIndex, columnIndex).Style, cellStyles);
                        }
                    }
                    else if (!string.IsNullOrEmpty(value))
                    {
                        if (firstDataColumnIndex == 0)
                            firstDataColumnIndex = columnIndex;
                        worksheet.Cell(rowIndex, columnIndex).Value = value;
                        if (property.Key == "Група")
                            isGroupTitle = true;

                        // Apply cell styles for regular cells
                        if (!isGroupTitle && cellStyles != null)
                            ApplyStyles(worksheet.Cell(rowIndex, columnIndex).Style, cellStyles);
                    }
                    
                    columnIndex++;
                }

                // Apply style to the row if it's a group title
                if (isGroupTitle)
                    {
                        IXLRange rowRange = worksheet.Range(rowIndex, firstDataColumnIndex, rowIndex, columnIndex - 1);

                        var styles = groupTitleStyles != null
                            ? new Dictionary<string, object>(groupTitleStyles)
                            : new Dictionary<string, object>();

                        if (styles.ContainsKey("BackgroundColor") && styles["BackgroundColor"] is XLColor baseColor)
                        {
                            // Calculate the shade factor based on depth (currentIdentitySize)
                            int depth = Math.Min(currentIdentitySize, 255);
                            double factor = 1.0 - (depth / 255.0); // 1.0 (base color) to 0.0 (white)

                            // Interpolate each RGB channel towards white
                            int r = (int)(baseColor.Color.R * factor + 255 * (1 - factor));
                            int g = (int)(baseColor.Color.G * factor + 255 * (1 - factor));
                            int b = (int)(baseColor.Color.B * factor + 255 * (1 - factor));

                            var shadeColor = XLColor.FromArgb(r, g, b);
                            styles["BackgroundColor"] = shadeColor;
                        }

                        ApplyStyles(rowRange.Style, styles);
                    }
                // if (isGroupTitle)
                // {
                //     IXLRange rowRange = worksheet.Range(rowIndex, firstDataColumnIndex, rowIndex, columnIndex - 1);
                //     if (groupTitleStyles != null)
                //     {
                //         if (groupTitleStyles.ContainsKey("BackgroundColor"))
                //         {
                //             groupTitleStyles["BackgroundColor"]=((XLColor)groupTitleStyles["BackgroundColor"]).GetShade(-0.05);

                //         }
                //         ApplyStyles(rowRange.Style, groupTitleStyles);
                //     }
                // }

                rowIndex++;
            }

             // Auto-fit columns after adding data
            worksheet.Columns(2,20).AdjustToContents();

            return workbook;
        }
        private void ApplyStyles(IXLStyle style, Dictionary<string, object> styleProperties)
        {
            foreach (var property in styleProperties)
            {
                switch (property.Key)
                {
                    case "FontBold":
                        style.Font.Bold = (bool)property.Value;
                        break;
                    case "FontSize":
                        if (property.Value is int intValue)
                        {
                            style.Font.FontSize = Convert.ToDouble(intValue); // Explicit cast
                        }
                        else if (property.Value is double doubleValue)
                        {
                            style.Font.FontSize = doubleValue;
                        }
                        else if (property.Value is string stringValue && double.TryParse(stringValue, out double parsedDouble))
                        {
                            style.Font.FontSize = parsedDouble;
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid FontSize value: {property.Value}");
                        }
                        break;
                    case "FontColor":
                        style.Font.FontColor = (XLColor)property.Value;
                        break;
                    case "HorizontalAlignment":
                        style.Alignment.Horizontal = (XLAlignmentHorizontalValues)property.Value;
                        break;
                    case "BackgroundColor":
                        style.Fill.BackgroundColor = (XLColor)property.Value;
                        break;
                    case "BottomBorder":
                        style.Border.BottomBorder = (XLBorderStyleValues)property.Value;
                        break;
                        // Add more cases as needed for other styles
                }
            }
        }

        public XLWorkbook GenerateExcelReport(List<dynamic> data, string worksheetName)
        {

            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add(worksheetName.Substring(0,Math.Min(worksheetName.Length,30)));


            var properties = ((IDictionary<string, object>)data[0]).Keys;
            int columnIndex = 1;
            foreach (var propertyName in properties)
            {
                worksheet.Cell(1, columnIndex).Value = propertyName;
                columnIndex++;
            }

            columnIndex--; // to get to the last used column

            // style
            worksheet.Columns(1, columnIndex).Width = 16;
            worksheet.Columns(1, columnIndex).Style.Alignment.SetWrapText(true);
            worksheet.Range(1, 1, 1, columnIndex).Style.Alignment.SetHorizontal((XLAlignmentHorizontalValues)XLDrawingHorizontalAlignment.Justify); // horizontal align of the text  
            worksheet.Range(1, 1, 1, columnIndex).Style.Alignment.SetVertical((XLAlignmentVerticalValues)XLDrawingVerticalAlignment.Justify); // vertical align of the text 


            // Add data
            int rowIndex = 2;
            foreach (var item in data)
            {
                columnIndex = 1;
                foreach (var property in (IDictionary<string, object>)item)
                {
                    object value = property.Value;
                    switch (value)
                    {
                        case DateTime:
                            worksheet.Cell(rowIndex, columnIndex).SetValue((DateTime)value);
                            break;
                        case bool:
                            worksheet.Cell(rowIndex, columnIndex).SetValue((bool)value);
                            break;
                        case int:
                            worksheet.Cell(rowIndex, columnIndex).SetValue((int)value);
                            break;
                        case double:
                            worksheet.Cell(rowIndex, columnIndex).SetValue((double)value);
                            break;
                        case decimal:
                            worksheet.Cell(rowIndex, columnIndex).SetValue((decimal)value);
                            break;
                        default:
                            worksheet.Cell(rowIndex, columnIndex).Value = value?.ToString();
                            break;
                    }

                    columnIndex++;
                }

                rowIndex++;
            }

            return workbook;
        }


        public XLWorkbook GenerateExcelReport(List<dynamic> data, string worksheetName, string header, Dictionary<string, object> styleProperties = null)
        {
            XLWorkbook workbook = GenerateExcelReport(data, worksheetName);

            var worksheet = workbook.Worksheet(1);  // Assuming you're working with the first sheet

            worksheet.Row(1).InsertRowsAbove(2);

            ClosedXML.Excel.IXLCell iXLCell = worksheet.Cell(1, 1);

            iXLCell.Value = header;
            IXLStyle style = iXLCell.Style;

            // Apply the style properties (if provided)
            if (styleProperties != null)
            {
                // Apply each style property from the dictionary
                foreach (var property in styleProperties)
                {
                    switch (property.Key)
                    {
                        case "FontBold":
                            style.Font.Bold = (bool)property.Value;
                            break;
                        case "FontSize":
                            if (property.Value is int intValue)
                            {
                                style.Font.FontSize = Convert.ToDouble(intValue); // Explicit cast
                            }
                            else if (property.Value is double doubleValue)
                            {
                                style.Font.FontSize = doubleValue;
                            }
                            else if (property.Value is string stringValue && double.TryParse(stringValue, out double parsedDouble))
                            {
                                style.Font.FontSize = parsedDouble;
                            }
                            else
                            {


                                throw new ArgumentException($"Invalid FontSize value: {property.Value}");

                            }
                            break;
                        case "FontColor":
                            style.Font.FontColor = (XLColor)property.Value;
                            break;
                        case "HorizontalAlignment":
                            style.Alignment.Horizontal = (XLAlignmentHorizontalValues)property.Value;
                            break;
                        case "BackgroundColor":
                            style.Fill.BackgroundColor = (XLColor)property.Value;
                            break;
                        case "BottomBorder":
                            style.Border.BottomBorder = (XLBorderStyleValues)property.Value;
                            break;
                            // Add more cases as needed for other styles
                    }
                }
            }


            return workbook;
        }
    }
}
