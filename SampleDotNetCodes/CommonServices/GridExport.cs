using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Reflection;
using System.Net;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.IO;
using Common.Helpers;
using Common.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Linq;

namespace Common.Services
{
    public class GridExport
    {
        string _xlsxFilePath;
        string _title;
        string _subtitle;
        XDocument _fileSchemaXDoc;
        double _total;
        double _convertedDouble;

        public GridExport(string title, string fileName, string subtitle = "", XDocument fileSchemaXDoc= null)
        {
            _subtitle = subtitle;
            _title = title;
            _xlsxFilePath = fileName;
            _fileSchemaXDoc = fileSchemaXDoc;
        }

        public bool CreateExcelDocument<T>(List<T> list, GridPaginationOptions paginationOpts, string outputFolder = null, XDocument fileSchemaXDoc = null)
        {
            DataSet ds = new DataSet();
            ds.Tables.Add(ListToDataTable(list, paginationOpts, fileSchemaXDoc));
            return CreateExcelDocument(ds, outputFolder);
        }

        public DataTable ListToDataTable<T>(List<T> list, GridPaginationOptions paginationOpts, XDocument fileSchemaXDoc = null)
        {
            DataTable dt = new DataTable();

            foreach (string name in paginationOpts.Names)
            {
                dt.Columns.Add(new DataColumn(name, name.GetType())); //System.String
            }

            foreach (T t in list)
            {
                DataRow row = dt.NewRow();
                foreach (PropertyInfo info in typeof(T).GetProperties())
                {
                    int index = paginationOpts.Fields.FindIndex(s => s.ToUpper().Equals(info.Name.ToUpper()));
                    if (index != -1)
                    {
                        if (info.Name == "Program")
                        {
                            var program = info.GetValue(t, null).ToString();
                            List<FtpFileColumnMetadata> dataColumns = new List<FtpFileColumnMetadata>();
                            XNode dataNode = fileSchemaXDoc.Root.Elements().First().Elements().Where(w=>w.Attribute("name").Value.ToString().ToLower() == "program") .FirstOrDefault();
                            dataColumns.AddRange(ImportHelper.GetFtpFileColumnAttributes(dataNode));
                            var optionValues = dataColumns.Where(w => w.name.ToLower() == "program").FirstOrDefault().options.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            Dictionary<string, string> dictionary = optionValues.Length > 0 ?  optionValues.ToDictionary(s => s.Split(':')[0].ToUpper(), s => s.Split(':')[1]) : new Dictionary<string, string>();
                            row[index] = optionValues.Length > 0  && dictionary.Count>0 ?  (dictionary.ContainsKey(program.ToUpper()) ? dictionary[program.ToUpper()] : "") : program ;
                        }
                        else
                        {
                            row[index] = (info.GetValue(t, null) ?? DBNull.Value);
                        }                    
                    }
                }
                dt.Rows.Add(row);
            }
            return dt;
        }
       
        public bool CreateExcelDocument(DataTable dt)
        {
            DataSet ds = new DataSet();
            ds.Tables.Add(dt);
            bool result = CreateExcelDocument(ds);
            ds.Tables.Remove(dt);
            return result;
        }

        public bool CreateExcelDocument(DataSet ds, string outputFolder = null)
        {
            try
            {
                DirectoryInfo folder = new DirectoryInfo(outputFolder == null ? AppSetting.DownloadExcelFolder : outputFolder);
                using (SpreadsheetDocument document = SpreadsheetDocument.Create(folder.FullName + _xlsxFilePath, SpreadsheetDocumentType.Workbook))
                {

                    WriteExcelFile(ds, document);
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void WriteExcelFile(DataSet ds, SpreadsheetDocument spreadsheet)
        {
            spreadsheet.AddWorkbookPart();
            spreadsheet.WorkbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();

            spreadsheet.WorkbookPart.Workbook.Append(new BookViews(new WorkbookView()));


            WorkbookStylesPart workbookStylesPart = spreadsheet.WorkbookPart.AddNewPart<WorkbookStylesPart>("rIdStyles");
            Stylesheet stylesheet = GenerateStyleSheet();
            workbookStylesPart.Stylesheet = stylesheet;
            spreadsheet.WorkbookPart.Workbook.Save();

            uint worksheetNumber = 1;
            foreach (DataTable dt in ds.Tables)
            {
                string workSheetID = "rId" + worksheetNumber.ToString();
                string worksheetName = dt.TableName;

                WorksheetPart newWorksheetPart = spreadsheet.WorkbookPart.AddNewPart<WorksheetPart>();
                newWorksheetPart.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet();

                newWorksheetPart.Worksheet.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.SheetData());

                WriteDataTableToExcelWorksheet(dt, newWorksheetPart);

                newWorksheetPart.Worksheet.Save();

                if (worksheetNumber == 1)
                    spreadsheet.WorkbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());

                spreadsheet.WorkbookPart.Workbook.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Sheets>().AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheet()
                {
                    Id = spreadsheet.WorkbookPart.GetIdOfPart(newWorksheetPart),
                    SheetId = (uint)worksheetNumber,
                    Name = dt.TableName
                });

                worksheetNumber++;
            }

        }

        private void WriteDataTableToExcelWorksheet(DataTable dt, WorksheetPart worksheetPart)
        {
            var worksheet = worksheetPart.Worksheet;
            var sheetData = worksheet.GetFirstChild<SheetData>();
            string cellValue = "";
            int numberOfColumns = dt.Columns.Count;
            bool[] IsNumericColumn = new bool[numberOfColumns];

            string[] excelColumnNames = new string[numberOfColumns];
            for (int n = 0; n < numberOfColumns; n++)
                excelColumnNames[n] = GetExcelColumnName(n);

            uint rowIndex = 1;
            Columns columns = AutoSize(sheetData);

            if (!string.IsNullOrEmpty(_title))
            {
                AppendTextCell(excelColumnNames[0] + rowIndex.ToString(), _title, new Row { RowIndex = rowIndex }, 3, ref sheetData);
            }

            if (!string.IsNullOrEmpty(_subtitle))
            {
                string[] tokens = _subtitle.Split('\n');
                foreach (string token in tokens)
                {
                    rowIndex++;
                    AppendTextCell(excelColumnNames[0] + rowIndex.ToString(), token, new Row { RowIndex = rowIndex }, 2, ref sheetData);
                }
            }

            if (!string.IsNullOrEmpty(_title) || !string.IsNullOrEmpty(_subtitle))
            {
                rowIndex++;
                sheetData.Append(new Row { RowIndex = rowIndex }); //blank line
                rowIndex++;
            }

            var headerRow = new Row { RowIndex = rowIndex };
            sheetData.Append(headerRow);

            for (int colInx = 0; colInx < numberOfColumns; colInx++)
            {
                DataColumn col = dt.Columns[colInx];
                AppendTextCell(excelColumnNames[colInx] + rowIndex.ToString(), col.ColumnName, headerRow, 4, ref sheetData, true);
                IsNumericColumn[colInx] = (col.DataType.FullName == "System.Decimal") || (col.DataType.FullName == "System.Int32");
            }

            var sumColumn = _fileSchemaXDoc.Root.Elements("Detail").Elements().Where(w => (string)w.Attribute("options") == "SUM").Select(s => s.Attribute("no") == null ? "0" : s.Attribute("no").Value).FirstOrDefault();

            foreach (DataRow dr in dt.Rows)
            {
                ++rowIndex;
                var newExcelRow = new Row { RowIndex = rowIndex };
                sheetData.Append(newExcelRow);

                for (int colInx = 0; colInx < numberOfColumns; colInx++)
                {
                    cellValue = dr.ItemArray[colInx].ToString();

                    if (sumColumn == (colInx + 1).ToString())
                    {
                        double.TryParse(cellValue, out _convertedDouble);
                        _total = _total + _convertedDouble;
                    }

                    if (IsNumericColumn[colInx])
                    {
                        if (double.TryParse(cellValue, out _convertedDouble))
                        {
                            cellValue = _convertedDouble.ToString();
                            AppendNumericCell(excelColumnNames[colInx] + rowIndex.ToString(), cellValue, newExcelRow, ref sheetData, true);
                        }
                    }
                    else
                    {
                        AppendTextCell(excelColumnNames[colInx] + rowIndex.ToString(), cellValue, newExcelRow, 2, ref sheetData, true);
                    }
                }
            }

            //Tail filling
            var tailRows = _fileSchemaXDoc.Root.Elements("Tail").ToList();
            foreach (var tailRow in tailRows)
            {
                ++rowIndex;
                var newExcelRow = new Row { RowIndex = rowIndex };
                sheetData.Append(newExcelRow);
                int colInx = 0;
                foreach (var col in tailRow.Elements())
                {
                    cellValue = col.Attribute("value") != null ? col.Attribute("value").Value : col.Value.ToString();

                    switch (col.Attribute("name").Value)
                    {
                        case "TotalAmount":
                            cellValue = _total.ToString();
                            break;
                        case "CreateDate":
                            cellValue = cellValue + DateTime.Now.ToString("MM/dd/yyy");
                            break;
                        default:
                            break;
                    }

                    if (IsNumericColumn[colInx])
                    {
                       AppendNumericCell(excelColumnNames[colInx] + rowIndex.ToString(), cellValue, newExcelRow, ref sheetData, true);
                    }
                    else
                    {
                        AppendTextCell(excelColumnNames[colInx] + rowIndex.ToString(), cellValue, newExcelRow, 2, ref sheetData, true);
                    }
                    colInx++;
                }
            }
        }

        private void AppendTextCell(string cellReference, string cellStringValue, Row excelRow, int styleIndex, ref SheetData sheetData, bool ignoreSheetAppend = false)
        {
            if (!ignoreSheetAppend)
                sheetData.Append(excelRow);

            Cell cell = new Cell() { CellReference = cellReference, DataType = CellValues.String, StyleIndex = (UInt32)styleIndex };
            CellValue cellValue = new CellValue();
            cellValue.Text = cellStringValue;
            cell.Append(cellValue);
            excelRow.Append(cell);
        }

        private void AppendNumericCell(string cellReference, string cellStringValue, Row excelRow, ref SheetData sheetData, bool ignoreSheetAppend = false)
        {
            if (!ignoreSheetAppend)
                sheetData.Append(excelRow);
            Cell cell = new Cell() { CellReference = cellReference };
            CellValue cellValue = new CellValue();
            cellValue.Text = cellStringValue;
            cell.Append(cellValue);
            excelRow.Append(cell);
        }

        private string GetExcelColumnName(int columnIndex)
        {
            if (columnIndex < 26)
                return ((char)('A' + columnIndex)).ToString();

            char firstChar = (char)('A' + (columnIndex / 26) - 1);
            char secondChar = (char)('A' + (columnIndex % 26));

            return string.Format("{0}{1}", firstChar, secondChar);
        }

        private Stylesheet GenerateStyleSheet()
        {
            return new Stylesheet(
                new Fonts(
                    new Font(                                                               // Index 0 – The default font.
                        new FontSize() { Val = 11 },
                        new Color() { Rgb = new HexBinaryValue() { Value = "000000" } },
                        new FontName() { Val = "Arial" }),
                    new Font(                                                               // Index 1 – The bold font.
                        new Bold(),
                        new FontSize() { Val = 11 },
                        new Color() { Rgb = new HexBinaryValue() { Value = "000000" } },
                        new FontName() { Val = "Arial" }),
                    new Font(                                                               // Index 2 – The Italic font.
                        new Italic(),
                        new FontSize() { Val = 11 },
                        new Color() { Rgb = new HexBinaryValue() { Value = "000000" } },
                        new FontName() { Val = "Arial" }),
                    new Font(                                                               // Index 3 – The Times Roman font. with 16 size
                        new FontSize() { Val = 18 },
                        new Color() { Rgb = new HexBinaryValue() { Value = "000000" } },
                        new FontName() { Val = "Arial" })
                ),
                new Fills(
                    new Fill(                                                           // Index 0 – The default fill.
                        new PatternFill() { PatternType = PatternValues.None }),
                    new Fill(                                                           // Index 1 – The default fill of gray 125 (required)
                        new PatternFill() { PatternType = PatternValues.Gray125 }),
                    new Fill(                                                           // Index 2 – The yellow fill.
                        new PatternFill(
                            new ForegroundColor() { Rgb = new HexBinaryValue() { Value = "abd4f9" } }
                        )
                        { PatternType = PatternValues.Solid })
                ),
                new Borders(
                    new Border(                                                         // Index 0 – The default border.
                        new LeftBorder(),
                        new RightBorder(),
                        new TopBorder(),
                        new BottomBorder(),
                        new DiagonalBorder()),
                    new Border(                                                         // Index 1 – Applies a Left, Right, Top, Bottom border to a cell
                        new LeftBorder(
                            new Color() { Auto = true }
                        )
                        { Style = BorderStyleValues.Thin },
                        new RightBorder(
                            new Color() { Auto = true }
                        )
                        { Style = BorderStyleValues.Thin },
                        new TopBorder(
                            new Color() { Auto = true }
                        )
                        { Style = BorderStyleValues.Thin },
                        new BottomBorder(
                            new Color() { Auto = true }
                        )
                        { Style = BorderStyleValues.Thin },
                        new DiagonalBorder())
                ),
                new CellFormats(
                    new CellFormat() { FontId = 0, FillId = 0, BorderId = 0 },                          // Index 0 – The default cell style.  If a cell does not have a style index applied it will use this style combination instead
                    new CellFormat() { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true },       // Index 1 – Bold 
                    new CellFormat() { FontId = 2, FillId = 0, BorderId = 0, ApplyFont = true },       // Index 2 – Italic
                    new CellFormat() { FontId = 3, FillId = 0, BorderId = 0, ApplyFont = true },       // Index 3 – Times Roman
                    new CellFormat() { FontId = 0, FillId = 2, BorderId = 0, ApplyFill = true },       // Index 4 – Yellow Fill
                    new CellFormat(                                                                   // Index 5 – Alignment
                        new Alignment() { Horizontal = HorizontalAlignmentValues.Center, Vertical = VerticalAlignmentValues.Center }
                    )
                    { FontId = 0, FillId = 0, BorderId = 0, ApplyAlignment = true },
                    new CellFormat() { FontId = 0, FillId = 0, BorderId = 1, ApplyBorder = true }      // Index 6 – Border
                )
            );
        }

        private Columns AutoSize(SheetData sheetData)
        {
            var maxColWidth = GetMaxCharacterWidth(sheetData);

            Columns columns = new Columns();
            double maxWidth = 7;
            foreach (var item in maxColWidth)
            {
                double width = Math.Truncate((item.Value * maxWidth + 5) / maxWidth * 256) / 256;
                double pixels = Math.Truncate(((256 * width + Math.Truncate(128 / maxWidth)) / 256) * maxWidth);
                double charWidth = Math.Truncate((pixels - 5) / maxWidth * 100 + 0.5) / 100;
                Column col = new Column() { BestFit = true, Min = (UInt32)(item.Key + 1), Max = (UInt32)(item.Key + 1), CustomWidth = true, Width = (DoubleValue)width };
                columns.Append(col);
            }

            return columns;
        }

        private Dictionary<int, int> GetMaxCharacterWidth(SheetData sheetData)
        {
            Dictionary<int, int> maxColWidth = new Dictionary<int, int>();
            var rows = sheetData.Elements<Row>();
            UInt32[] numberStyles = new UInt32[] { 5, 6, 7, 8 };
            UInt32[] boldStyles = new UInt32[] { 1, 2, 3, 4, 6, 7, 8 };
            foreach (var r in rows)
            {
                var cells = r.Elements<Cell>().ToArray();

                for (int i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i];
                    var cellValue = cell.CellValue == null ? string.Empty : cell.CellValue.InnerText;
                    var cellTextLength = cellValue.Length;

                    if (cell.StyleIndex != null && numberStyles.Contains(cell.StyleIndex))
                    {
                        int thousandCount = (int)Math.Truncate((double)cellTextLength / 4);

                        cellTextLength += (3 + thousandCount);
                    }

                    if (cell.StyleIndex != null && boldStyles.Contains(cell.StyleIndex))
                    {
                        cellTextLength += 1;
                    }

                    if (maxColWidth.ContainsKey(i))
                    {
                        var current = maxColWidth[i];
                        if (cellTextLength > current)
                        {
                            maxColWidth[i] = cellTextLength;
                        }
                    }
                    else
                    {
                        maxColWidth.Add(i, cellTextLength);
                    }
                }
            }

            return maxColWidth;
        }

        public HttpResponseMessage DownloadFile(string fileName)
        {
            FileInfo fileInfo = new FileInfo(AppSetting.DownloadExcelFolder + fileName); //xlsx
            HttpResponseMessage httpResponseMessage = new HttpResponseMessage();
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (FileStream file = new FileStream(AppSetting.DownloadExcelFolder + fileName, FileMode.Open, FileAccess.Read))
                    {
                        byte[] bytes = new byte[file.Length];
                        file.Read(bytes, 0, (int)file.Length);
                        ms.Write(bytes, 0, (int)file.Length);

                        httpResponseMessage.Content = new ByteArrayContent(bytes);
                        httpResponseMessage.Content.Headers.Add("x-filename", fileName);
                        httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ms-excel");
                        httpResponseMessage.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                        httpResponseMessage.Content.Headers.ContentDisposition.FileName = fileInfo.FullName;
                        httpResponseMessage.StatusCode = HttpStatusCode.OK;
                    }
                }
            }
            catch (System.Exception ex)
            {
                httpResponseMessage.StatusCode = HttpStatusCode.InternalServerError;
            }
            return httpResponseMessage;

        }

    }
}