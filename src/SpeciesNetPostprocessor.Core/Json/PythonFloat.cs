using System.Globalization;
using System.Text;

namespace SpeciesNetPostprocessor.Core.Json;

/// <summary>
/// Formats doubles the way Python's repr() (and therefore json.dump()) does.
/// </summary>
public static class PythonFloat
{
    /// <summary>
    /// Returns the Python repr() of [d], e.g. "0.5", "1.0", "1e-05", "1e+16".  Uses the shortest
    /// digit string that round-trips, and (like Python's float_repr_style 'short') switches to
    /// exponential notation when the decimal exponent is < -4 or >= 16.  Non-finite values are
    /// formatted the way json.dump() formats them.
    /// </summary>
    public static string Repr(double d)
    {
        if (double.IsNaN(d))
        {
            return "NaN";
        }
        if (double.IsPositiveInfinity(d))
        {
            return "Infinity";
        }
        if (double.IsNegativeInfinity(d))
        {
            return "-Infinity";
        }
        if (d == 0)
        {
            return double.IsNegative(d) ? "-0.0" : "0.0";
        }

        // .NET's "R" format produces the shortest round-trippable digit string; we only need
        // to re-arrange those digits.
        string s = d.ToString("R", CultureInfo.InvariantCulture);

        bool negative = s[0] == '-';
        if (negative)
        {
            s = s.Substring(1);
        }

        int exponent = 0;
        int ePosition = s.IndexOfAny(['E', 'e']);
        if (ePosition >= 0)
        {
            exponent = int.Parse(s.AsSpan(ePosition + 1), NumberStyles.AllowLeadingSign,
                                 CultureInfo.InvariantCulture);
            s = s.Substring(0, ePosition);
        }

        string integerPart = s;
        string fractionPart = string.Empty;
        int dotPosition = s.IndexOf('.');
        if (dotPosition >= 0)
        {
            integerPart = s.Substring(0, dotPosition);
            fractionPart = s.Substring(dotPosition + 1);
        }

        // Value is 0.[digits] * 10^decimalPoint
        string digits = integerPart + fractionPart;
        int decimalPoint = integerPart.Length + exponent;

        int firstNonZero = 0;
        while (firstNonZero < digits.Length && digits[firstNonZero] == '0')
        {
            firstNonZero++;
        }
        digits = digits.Substring(firstNonZero);
        decimalPoint -= firstNonZero;
        digits = digits.TrimEnd('0');

        var sb = new StringBuilder();
        if (negative)
        {
            sb.Append('-');
        }

        if (decimalPoint <= -4 || decimalPoint > 16)
        {
            sb.Append(digits[0]);
            if (digits.Length > 1)
            {
                sb.Append('.');
                sb.Append(digits, 1, digits.Length - 1);
            }
            int e = decimalPoint - 1;
            sb.Append('e');
            sb.Append(e < 0 ? '-' : '+');
            sb.Append(Math.Abs(e).ToString("00", CultureInfo.InvariantCulture));
        }
        else if (decimalPoint <= 0)
        {
            sb.Append("0.");
            sb.Append('0', -decimalPoint);
            sb.Append(digits);
        }
        else if (decimalPoint >= digits.Length)
        {
            sb.Append(digits);
            sb.Append('0', decimalPoint - digits.Length);
            sb.Append(".0");
        }
        else
        {
            sb.Append(digits, 0, decimalPoint);
            sb.Append('.');
            sb.Append(digits, decimalPoint, digits.Length - decimalPoint);
        }

        return sb.ToString();
    }
}
