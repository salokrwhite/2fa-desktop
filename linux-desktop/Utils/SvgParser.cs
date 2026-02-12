using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace TwoFactorAuth.Utils;

public static class SvgParser
{
    private const string SvgNs = "http://www.w3.org/2000/svg";

    private static readonly string[] ShapeTags =
        { "path", "rect", "circle", "ellipse", "polygon", "polyline", "line" };


    public static bool IsValidSvg(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            return content.Contains("<svg", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static (string? PathData, string? Color) ParseSvgFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            return ParseSvgContent(content);
        }
        catch (Exception ex)
        {
            Log($"ReadFile failed: {ex.Message}");
            return (null, null);
        }
    }

    public static (string? PathData, string? Color) ParseSvgContent(string svgContent)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(svgContent);
            var cssColors = ParseCssStyles(doc);
            var segments = new List<string>();
            var colorVotes = new List<string>(); 

            CollectShapes(doc.DocumentElement!, segments, colorVotes, cssColors, Matrix.Identity);
            string? color = null;
            if (colorVotes.Count > 0)
            {
                color = colorVotes
                    .GroupBy(c => c.ToUpperInvariant())
                    .OrderByDescending(g => g.Count())
                    .First().Key;
            }

            if (string.IsNullOrEmpty(color) || color == "none")
                color = doc.DocumentElement?.Attributes?["fill"]?.Value;

            var pathData = segments.Count > 0 ? string.Join(' ', segments) : null;
            color = CleanColor(color);

            var distinctCount = colorVotes.Select(c => c.ToUpperInvariant()).Distinct().Count();
            Log($"segments={segments.Count}, pathLen={pathData?.Length ?? 0}, colors={colorVotes.Count}, distinct={distinctCount}, result={color ?? "null"}");
            return (pathData, color);
        }
        catch (Exception ex)
        {
            Log($"XML failed: {ex.Message}, trying regex");
            return ParseSvgWithRegex(svgContent);
        }
    }


    private static void CollectShapes(
        XmlNode node,
        List<string> segments,
        List<string> colorVotes,
        Dictionary<string, string> cssColors,
        Matrix parentTransform)
    {
        if (node.NodeType != XmlNodeType.Element) return;
        var localTransform = ParseTransform(node.Attributes?["transform"]?.Value);
        var combined = parentTransform.Multiply(localTransform);
        var tag = node.LocalName.ToLowerInvariant();
        if (ShapeTags.Contains(tag))
        {
            var d = ConvertToPathData(node);
            if (!string.IsNullOrEmpty(d))
            {
                if (!combined.IsIdentity)
                    d = ApplyTransformToPath(d, combined);

                segments.Add(d);
            }

            var c = ResolveColor(node, cssColors);
            if (!string.IsNullOrEmpty(c) && c != "none" && c != "transparent")
                colorVotes.Add(c);
        }

        foreach (XmlNode child in node.ChildNodes)
            CollectShapes(child, segments, colorVotes, cssColors, combined);
    }


    private static Dictionary<string, string> ParseCssStyles(XmlDocument doc)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var styleNodes = doc.GetElementsByTagName("style", SvgNs);
            if (styleNodes.Count == 0)
                styleNodes = doc.GetElementsByTagName("style");

            foreach (XmlNode styleNode in styleNodes)
            {
                var css = styleNode.InnerText;
                var matches = Regex.Matches(css, @"\.([a-zA-Z0-9_-]+)\s*\{([^}]+)\}");
                foreach (Match m in matches)
                {
                    var className = m.Groups[1].Value;
                    var body = m.Groups[2].Value;
                    var fillMatch = Regex.Match(body, @"fill:\s*([^;}\s]+)");
                    if (fillMatch.Success)
                        result[className] = fillMatch.Groups[1].Value.Trim();
                }
            }
        }
        catch {  }
        return result;
    }

    private static string? ResolveColor(XmlNode node, Dictionary<string, string> cssColors)
    {
        var fill = node.Attributes?["fill"]?.Value;
        if (!string.IsNullOrEmpty(fill) && fill != "none") return fill;
        var style = node.Attributes?["style"]?.Value;
        if (!string.IsNullOrEmpty(style))
        {
            var m = Regex.Match(style, @"fill:\s*([^;]+)");
            if (m.Success)
            {
                var v = m.Groups[1].Value.Trim();
                if (v != "none") return v;
            }
        }

        var cls = node.Attributes?["class"]?.Value;
        if (!string.IsNullOrEmpty(cls))
        {
            foreach (var c in cls.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (cssColors.TryGetValue(c, out var cssColor))
                    return cssColor;
            }
        }

        return null;
    }


    private static string? ConvertToPathData(XmlNode node)
    {
        return node.LocalName.ToLowerInvariant() switch
        {
            "path"     => node.Attributes?["d"]?.Value,
            "rect"     => RectToPath(node),
            "circle"   => CircleToPath(node),
            "ellipse"  => EllipseToPath(node),
            "polygon"  => PolygonToPath(node, close: true),
            "polyline" => PolygonToPath(node, close: false),
            "line"     => LineToPath(node),
            _ => null
        };
    }

    private static string? RectToPath(XmlNode node)
    {
        var x = A(node, "x"); var y = A(node, "y");
        var w = A(node, "width"); var h = A(node, "height");
        var rx = A(node, "rx"); var ry = A(node, "ry");
        if (w <= 0 || h <= 0) return null;
        if (rx <= 0 && ry <= 0)
            return $"M{S(x)},{S(y)}h{S(w)}v{S(h)}h{S(-w)}Z";
        if (rx <= 0) rx = ry; if (ry <= 0) ry = rx;
        rx = Math.Min(rx, w / 2); ry = Math.Min(ry, h / 2);
        return $"M{S(x + rx)},{S(y)}h{S(w - 2 * rx)}a{S(rx)},{S(ry)} 0 0 1 {S(rx)},{S(ry)}" +
               $"v{S(h - 2 * ry)}a{S(rx)},{S(ry)} 0 0 1 {S(-rx)},{S(ry)}" +
               $"h{S(-(w - 2 * rx))}a{S(rx)},{S(ry)} 0 0 1 {S(-rx)},{S(-ry)}" +
               $"v{S(-(h - 2 * ry))}a{S(rx)},{S(ry)} 0 0 1 {S(rx)},{S(-ry)}Z";
    }

    private static string? CircleToPath(XmlNode node)
    {
        var cx = A(node, "cx"); var cy = A(node, "cy"); var r = A(node, "r");
        if (r <= 0) return null;
        return $"M{S(cx - r)},{S(cy)}a{S(r)},{S(r)} 0 1 0 {S(2 * r)},0a{S(r)},{S(r)} 0 1 0 {S(-2 * r)},0Z";
    }

    private static string? EllipseToPath(XmlNode node)
    {
        var cx = A(node, "cx"); var cy = A(node, "cy");
        var rx = A(node, "rx"); var ry = A(node, "ry");
        if (rx <= 0 || ry <= 0) return null;
        return $"M{S(cx - rx)},{S(cy)}a{S(rx)},{S(ry)} 0 1 0 {S(2 * rx)},0a{S(rx)},{S(ry)} 0 1 0 {S(-2 * rx)},0Z";
    }

    private static string? PolygonToPath(XmlNode node, bool close)
    {
        var pts = node.Attributes?["points"]?.Value;
        if (string.IsNullOrWhiteSpace(pts)) return null;
        var nums = Regex.Matches(pts, @"-?[\d.]+");
        if (nums.Count < 4) return null;
        var sb = new StringBuilder();
        for (int i = 0; i < nums.Count - 1; i += 2)
        {
            sb.Append(i == 0 ? 'M' : 'L');
            sb.Append(nums[i].Value).Append(',').Append(nums[i + 1].Value);
        }
        if (close) sb.Append('Z');
        return sb.ToString();
    }

    private static string? LineToPath(XmlNode node)
    {
        return $"M{S(A(node, "x1"))},{S(A(node, "y1"))}L{S(A(node, "x2"))},{S(A(node, "y2"))}";
    }


    private struct Matrix
    {
        public double A, B, C, D, E, F;
        public static Matrix Identity => new() { A = 1, B = 0, C = 0, D = 1, E = 0, F = 0 };
        public bool IsIdentity => A == 1 && B == 0 && C == 0 && D == 1 && E == 0 && F == 0;

        public Matrix Multiply(Matrix m)
        {
            return new Matrix
            {
                A = A * m.A + C * m.B,
                B = B * m.A + D * m.B,
                C = A * m.C + C * m.D,
                D = B * m.C + D * m.D,
                E = A * m.E + C * m.F + E,
                F = B * m.E + D * m.F + F
            };
        }

        public (double x, double y) Apply(double x, double y)
        {
            return (A * x + C * y + E, B * x + D * y + F);
        }
    }

    private static Matrix ParseTransform(string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform)) return Matrix.Identity;

        var result = Matrix.Identity;
        var funcs = Regex.Matches(transform, @"(\w+)\s*\(([^)]+)\)");
        foreach (Match func in funcs)
        {
            var name = func.Groups[1].Value.ToLowerInvariant();
            var args = Regex.Matches(func.Groups[2].Value, @"-?[\d.]+(?:e[+-]?\d+)?")
                .Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();

            Matrix m = Matrix.Identity;
            switch (name)
            {
                case "translate":
                    m.E = args.Length > 0 ? args[0] : 0;
                    m.F = args.Length > 1 ? args[1] : 0;
                    break;
                case "scale":
                    m.A = args.Length > 0 ? args[0] : 1;
                    m.D = args.Length > 1 ? args[1] : m.A;
                    break;
                case "rotate":
                    if (args.Length >= 1)
                    {
                        var rad = args[0] * Math.PI / 180;
                        var cos = Math.Cos(rad);
                        var sin = Math.Sin(rad);
                        m.A = cos; m.B = sin; m.C = -sin; m.D = cos;
                        if (args.Length >= 3)
                        {
                            var cx = args[1]; var cy = args[2];
                            m.E = cx - cos * cx + sin * cy;
                            m.F = cy - sin * cx - cos * cy;
                        }
                    }
                    break;
                case "matrix":
                    if (args.Length >= 6)
                    {
                        m.A = args[0]; m.B = args[1]; m.C = args[2];
                        m.D = args[3]; m.E = args[4]; m.F = args[5];
                    }
                    break;
                case "skewx":
                    if (args.Length >= 1) m.C = Math.Tan(args[0] * Math.PI / 180);
                    break;
                case "skewy":
                    if (args.Length >= 1) m.B = Math.Tan(args[0] * Math.PI / 180);
                    break;
            }
            result = result.Multiply(m);
        }
        return result;
    }

    private static string ApplyTransformToPath(string pathData, Matrix mtx)
    {
        try
        {
            var sb = new StringBuilder();
            var tokens = Regex.Matches(pathData, @"[A-Za-z]|-?[\d.]+(?:e[+-]?\d+)?");
            int i = 0;
            char cmd = 'M';
            while (i < tokens.Count)
            {
                var t = tokens[i].Value;
                if (Regex.IsMatch(t, @"^[A-Za-z]$"))
                {
                    cmd = t[0];
                    sb.Append(cmd);
                    i++;
                    continue;
                }

                switch (char.ToUpper(cmd))
                {
                    case 'M': case 'L': case 'T':
                        if (i + 1 < tokens.Count)
                        {
                            var x = D(tokens[i].Value); var y = D(tokens[i + 1].Value);
                            var (tx, ty) = char.IsUpper(cmd) ? mtx.Apply(x, y) : ApplyDelta(mtx, x, y);
                            sb.Append($"{S(tx)},{S(ty)} ");
                            i += 2;
                        }
                        else i++;
                        break;
                    case 'H':
                        if (i < tokens.Count)
                        {
                            var x = D(tokens[i].Value);
                            var (tx, ty) = char.IsUpper(cmd) ? mtx.Apply(x, 0) : ApplyDelta(mtx, x, 0);
                            sb.Remove(sb.Length - 1, 1); 
                            sb.Append(char.IsUpper(cmd) ? 'L' : 'l');
                            sb.Append($"{S(tx)},{S(ty)} ");
                            i++;
                        }
                        else i++;
                        break;
                    case 'V':
                        if (i < tokens.Count)
                        {
                            var y = D(tokens[i].Value);
                            var (tx, ty) = char.IsUpper(cmd) ? mtx.Apply(0, y) : ApplyDelta(mtx, 0, y);
                            sb.Remove(sb.Length - 1, 1);
                            sb.Append(char.IsUpper(cmd) ? 'L' : 'l');
                            sb.Append($"{S(tx)},{S(ty)} ");
                            i++;
                        }
                        else i++;
                        break;
                    case 'C':
                        if (i + 5 < tokens.Count)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                var x = D(tokens[i].Value); var y = D(tokens[i + 1].Value);
                                var (tx, ty) = char.IsUpper(cmd) ? mtx.Apply(x, y) : ApplyDelta(mtx, x, y);
                                sb.Append($"{S(tx)},{S(ty)} ");
                                i += 2;
                            }
                        }
                        else i++;
                        break;
                    case 'S': case 'Q':
                        if (i + 3 < tokens.Count)
                        {
                            for (int j = 0; j < 2; j++)
                            {
                                var x = D(tokens[i].Value); var y = D(tokens[i + 1].Value);
                                var (tx, ty) = char.IsUpper(cmd) ? mtx.Apply(x, y) : ApplyDelta(mtx, x, y);
                                sb.Append($"{S(tx)},{S(ty)} ");
                                i += 2;
                            }
                        }
                        else i++;
                        break;
                    case 'A':
                        if (i + 6 < tokens.Count)
                        {
                            var rx = D(tokens[i].Value) * Math.Abs(mtx.A);
                            var ry = D(tokens[i + 1].Value) * Math.Abs(mtx.D);
                            var rot = D(tokens[i + 2].Value);
                            var la = tokens[i + 3].Value;
                            var sw = tokens[i + 4].Value;
                            var x = D(tokens[i + 5].Value); var y = D(tokens[i + 6].Value);
                            var (tx, ty) = char.IsUpper(cmd) ? mtx.Apply(x, y) : ApplyDelta(mtx, x, y);
                            sb.Append($"{S(rx)},{S(ry)} {S(rot)} {la} {sw} {S(tx)},{S(ty)} ");
                            i += 7;
                        }
                        else i++;
                        break;
                    case 'Z':
                        sb.Append(' ');
                        break;
                    default:
                        sb.Append(tokens[i].Value).Append(' ');
                        i++;
                        break;
                }
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return pathData; 
        }
    }

    private static (double, double) ApplyDelta(Matrix mtx, double dx, double dy)
    {
        return (mtx.A * dx + mtx.C * dy, mtx.B * dx + mtx.D * dy);
    }


    private static (string? PathData, string? Color) ParseSvgWithRegex(string svgContent)
    {
        try
        {
            var pathMatches = Regex.Matches(svgContent, @"<path[^>]*\sd=""([^""]+)""", RegexOptions.IgnoreCase);
            var sb = new StringBuilder();
            foreach (Match m in pathMatches)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(m.Groups[1].Value);
            }
            var pathData = sb.Length > 0 ? sb.ToString() : null;

            string? color = null;
            var fillMatch = Regex.Match(svgContent, @"fill=""([^""]+)""", RegexOptions.IgnoreCase);
            if (fillMatch.Success && fillMatch.Groups[1].Value != "none")
                color = fillMatch.Groups[1].Value;

            return (pathData, CleanColor(color));
        }
        catch { return (null, null); }
    }


    private static string? CleanColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color) || color == "none" || color == "transparent")
            return null;
        color = color.Trim();
        if (color.StartsWith("url(")) return null; 
        if (color.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var m = Regex.Match(color, @"rgba?\((\d+),\s*(\d+),\s*(\d+)");
            if (m.Success)
                return $"#{int.Parse(m.Groups[1].Value):X2}{int.Parse(m.Groups[2].Value):X2}{int.Parse(m.Groups[3].Value):X2}";
        }
        if (Regex.IsMatch(color, @"^[0-9A-Fa-f]{6}$")) return "#" + color.ToUpperInvariant();
        if (Regex.IsMatch(color, @"^#[0-9A-Fa-f]{3}$"))
            return $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}".ToUpperInvariant();
        return color;
    }


    private static double A(XmlNode node, string attr)
    {
        var val = node.Attributes?[attr]?.Value;
        if (string.IsNullOrEmpty(val)) return 0;
        val = Regex.Replace(val, @"[a-zA-Z%]+$", "");
        return double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private static double D(string val) =>
        double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static string S(double v) =>
        v.ToString("G", CultureInfo.InvariantCulture);

    private static void Log(string msg) =>
        System.Diagnostics.Debug.WriteLine($"[SvgParser] {msg}");
}
