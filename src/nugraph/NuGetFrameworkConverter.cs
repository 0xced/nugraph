using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NuGet.Frameworks;

namespace nugraph;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by Spectre.Console.Cli through reflection")]
internal sealed class NuGetFrameworkConverter : TypeConverter
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