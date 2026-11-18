using ClosedXML.Excel;
using KirkukInstitute.Models;

namespace KirkukInstitute.Services;

/// <summary>
/// Builds the students registry workbook. Rows are flattened from the relational graph:
/// Student → Hall (many-to-one), Student → StudentSubjects → Subject (many-to-many) and
/// Student → StudentTeachers → Teacher (many-to-many). Collections are joined into readable cells.
/// </summary>
public class StudentExportService
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#0F433E");
    private static readonly XLColor HeaderBorder = XLColor.FromHtml("#0B3431");
    private static readonly XLColor BandFill = XLColor.FromHtml("#F7F5EE");
    private static readonly XLColor GridLine = XLColor.FromHtml("#E3DBC7");
    private static readonly XLColor MutedText = XLColor.FromHtml("#64748B");

    private readonly Localizer _l;

    public StudentExportService(Localizer l) => _l = l;

    public byte[] Build(IReadOnlyList<Student> students)
    {
        var headers = new (string Title, double Width, bool Centered)[]
        {
            (_l.T("Student No.", "رقم الطالب"), 15, true),
            (_l.T("Full name (Arabic)", "الاسم الكامل (عربي)"), 26, false),
            (_l.T("Full name (English)", "الاسم الكامل (إنكليزي)"), 26, false),
            (_l.T("National ID", "الرقم الوطني"), 18, true),
            (_l.T("Gender", "الجنس"), 10, true),
            (_l.T("Date of birth", "تاريخ الميلاد"), 14, true),
            (_l.T("Mobile number", "رقم الهاتف"), 16, true),
            (_l.T("Residential address", "العنوان السكني"), 32, false),
            (_l.T("Hall", "القاعة"), 20, false),
            (_l.T("Grade", "الصف"), 20, false),
            (_l.T("Subjects", "المواد"), 44, false),
            (_l.T("Teachers", "المدرسون"), 40, false),
            (_l.T("Guardian", "ولي الأمر"), 26, false),
            (_l.T("Guardian phone", "هاتف ولي الأمر"), 16, true),
            (_l.T("Status", "الحالة"), 12, true),
            (_l.T("Registration date", "تاريخ التسجيل"), 15, true)
        };

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(_l.T("Students", "الطلاب"));
        sheet.RightToLeft = _l.IsArabic;
        sheet.ShowGridLines = false;
        sheet.Style.Font.FontName = "Arial";
        sheet.Style.Font.FontSize = 10;

        var columnCount = headers.Length;
        var lastColumn = ColumnLetter(columnCount);
        var headerRow = 3;
        var firstDataRow = headerRow + 1;
        var lastRow = Math.Max(headerRow, firstDataRow + students.Count - 1);

        BuildTitle(sheet, columnCount, students.Count);

        for (var c = 1; c <= columnCount; c++)
        {
            var header = sheet.Cell(headerRow, c);
            header.Value = headers[c - 1].Title;
            sheet.Column(c).Width = headers[c - 1].Width;
        }

        var headerRange = sheet.Range(headerRow, 1, headerRow, columnCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.FontSize = 10;
        headerRange.Style.Fill.BackgroundColor = HeaderFill;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        sheet.Row(headerRow).Height = 30;

        var row = firstDataRow;
        foreach (var s in students)
        {
            var subjects = string.Join(_l.IsArabic ? "، " : ", ",
                s.Subjects.Select(x => _l.Pick(x.Subject.NameAr, x.Subject.NameEn))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x, StringComparer.CurrentCulture));
            var teachers = string.Join(_l.IsArabic ? "، " : ", ",
                s.Teachers.Select(x => _l.Pick(x.Teacher.FullNameAr, x.Teacher.FullNameEn))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .OrderBy(x => x, StringComparer.CurrentCulture));

            sheet.Cell(row, 1).Value = s.StudentNo;
            sheet.Cell(row, 2).Value = s.FullNameAr;
            sheet.Cell(row, 3).Value = s.FullNameEn;
            sheet.Cell(row, 4).SetValue(s.NationalId ?? "");
            sheet.Cell(row, 5).Value = _l.E(s.Gender);
            sheet.Cell(row, 6).Value = s.BirthDate;
            sheet.Cell(row, 7).SetValue(s.Phone ?? "");
            sheet.Cell(row, 8).Value = s.Address;
            sheet.Cell(row, 9).Value = s.Hall is null
                ? ""
                : _l.Pick(s.Hall.NameAr, s.Hall.NameEn);
            sheet.Cell(row, 10).Value = _l.Pick(s.Grade.NameAr, s.Grade.NameEn);
            sheet.Cell(row, 11).Value = subjects;
            sheet.Cell(row, 12).Value = teachers;
            sheet.Cell(row, 13).Value = s.Parent is null
                ? ""
                : _l.Pick(s.Parent.FullNameAr, s.Parent.FullNameEn);
            sheet.Cell(row, 14).SetValue(s.Parent?.Phone ?? "");
            sheet.Cell(row, 15).Value = _l.E(s.Status);
            sheet.Cell(row, 16).Value = s.RegistrationDate;

            sheet.Cell(row, 4).Style.NumberFormat.Format = "@";
            sheet.Cell(row, 7).Style.NumberFormat.Format = "@";
            sheet.Cell(row, 14).Style.NumberFormat.Format = "@";
            sheet.Cell(row, 6).Style.DateFormat.Format = "yyyy-mm-dd";
            sheet.Cell(row, 16).Style.DateFormat.Format = "yyyy-mm-dd";

            row++;
        }

        if (students.Count > 0)
        {
            var dataRange = sheet.Range(firstDataRow, 1, lastRow, columnCount);
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            foreach (var centered in new[] { 1, 4, 5, 6, 7, 14, 15, 16 })
                sheet.Range(firstDataRow, centered, lastRow, centered).Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            // Soft banding keeps long subject/teacher lists readable without losing the official look.
            for (var r = firstDataRow + 1; r <= lastRow; r += 2)
                sheet.Range(r, 1, r, columnCount).Style.Fill.BackgroundColor = BandFill;

            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorderColor = GridLine;
        }

        var tableRange = sheet.Range(headerRow, 1, lastRow, columnCount);
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.OutsideBorderColor = GridLine;
        tableRange.SetAutoFilter();
        sheet.SheetView.FreezeRows(headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void BuildTitle(IXLWorksheet sheet, int columnCount, int count)
    {
        var lastColumn = ColumnLetter(columnCount);
        var titleRange = sheet.Range($"A1:{lastColumn}1");
        titleRange.Merge();
        var title = sheet.Cell("A1");
        title.Value = _l.T("Registered students report", "كشف الطلاب المسجلين");
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 16;
        title.Style.Font.FontColor = HeaderFill;
        title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        title.Style.Alignment.Horizontal = _l.IsArabic
            ? XLAlignmentHorizontalValues.Right
            : XLAlignmentHorizontalValues.Left;

        var subtitleRange = sheet.Range($"A2:{lastColumn}2");
        subtitleRange.Merge();
        var subtitle = sheet.Cell("A2");
        var date = _l.Date(DateTime.Today);
        subtitle.Value = _l.T($"{count} student(s) · exported {date}", $"{_l.Num(count)} طالباً · صُدّر في {date}");
        subtitle.Style.Font.FontSize = 10;
        subtitle.Style.Font.FontColor = MutedText;
        subtitle.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        subtitle.Style.Alignment.Horizontal = title.Style.Alignment.Horizontal;

        sheet.Row(1).Height = 26;
        sheet.Row(2).Height = 18;
    }

    private static string ColumnLetter(int column)
    {
        var letter = "";
        while (column > 0)
        {
            var modulo = (column - 1) % 26;
            letter = (char)('A' + modulo) + letter;
            column = (column - modulo) / 26;
        }
        return letter;
    }
}
