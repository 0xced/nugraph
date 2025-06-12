using System.ComponentModel;
using System.Globalization;
using NuGet.Frameworks;

namespace nugraph;

internal class NuGetFrameworkConverter : TypeConverter
{
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return NuGetFramework.Parse(stringValue);
        }

        return base.ConvertFrom(context, culture, value);
    }
}