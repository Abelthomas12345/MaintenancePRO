using System.Text;

namespace MaintenancePRO.Api.Services;

/// <summary>Générateur PDF minimal (texte, Helvetica, A4) sans dépendance externe.</summary>
public static class SimplePdf
{
    private const int MaxChars = 92, LinesPerPage = 50;

    public static byte[] Create(string title, IEnumerable<string> lines)
    {
        var all = new List<(string Text, bool Bold, int Size)> { (title, true, 16), ("", false, 11) };
        foreach (var raw in lines)
            foreach (var l in Wrap(Clean(raw))) all.Add((l, false, 11));

        var pages = new List<List<(string Text, bool Bold, int Size)>>();
        for (var i = 0; i < all.Count; i += LinesPerPage) pages.Add(all.GetRange(i, Math.Min(LinesPerPage, all.Count - i)));
        if (pages.Count == 0) pages.Add(new());

        var enc = Encoding.Latin1;
        var objects = new List<string>(); // index 0 => objet 1
        var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{5 + 2 * i} 0 R"));
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        for (var p = 0; p < pages.Count; p++)
        {
            var sb = new StringBuilder();
            var y = 800;
            foreach (var (text, bold, size) in pages[p])
            {
                sb.Append($"BT /{(bold ? "F2" : "F1")} {size} Tf 1 0 0 1 50 {y} Tm ({Escape(text)}) Tj ET\n");
                y -= size + holdGap(size);
            }
            var stream = sb.ToString();
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + 2 * p} 0 R >>");
            objects.Add($"<< /Length {enc.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }

        using var ms = new MemoryStream();
        void Write(string s) { var b = enc.GetBytes(s); ms.Write(b, 0, b.Length); }
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Write($"{o:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return ms.ToArray();

        static int holdGap(int size) => size > 12 ? 6 : 3;
    }

    private static string Clean(string s)
    {
        s = s.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"').Replace('–', '-').Replace('—', '-').Replace("…", "...").Replace("\r", "").Replace('\t', ' ');
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(c == '\n' || (c >= 32 && c <= 255) ? c : '?');
        return sb.ToString();
    }

    private static IEnumerable<string> Wrap(string text)
    {
        foreach (var para in text.Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in para.Split(' '))
            {
                var w = word;
                while (w.Length > MaxChars) { if (line.Length > 0) { yield return line.ToString(); line.Clear(); } yield return w[..MaxChars]; w = w[MaxChars..]; }
                if (line.Length + w.Length + 1 > MaxChars) { yield return line.ToString(); line.Clear(); }
                if (line.Length > 0) line.Append(' ');
                line.Append(w);
            }
            yield return line.ToString();
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
