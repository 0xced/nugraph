nugraph is a [.NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) for creating visual dependency graph of NuGet packages.

A picture is worth a thousand words, so here's the dependency graph produced by running `nugraph Microsoft.Extensions.Logging.Console`

```mermaid
graph LR

classDef root stroke-width:4px
classDef default fill:aquamarine,stroke:#009061,color:#333333

Microsoft.Extensions.Configuration --> Microsoft.Extensions.Configuration.Abstractions
Microsoft.Extensions.Configuration --> Microsoft.Extensions.Primitives
Microsoft.Extensions.Configuration.Abstractions --> Microsoft.Extensions.Primitives
Microsoft.Extensions.Configuration.Binder --> Microsoft.Extensions.Configuration.Abstractions
Microsoft.Extensions.DependencyInjection --> Microsoft.Extensions.DependencyInjection.Abstractions
Microsoft.Extensions.Logging --> Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Logging --> Microsoft.Extensions.Logging.Abstractions
Microsoft.Extensions.Logging --> Microsoft.Extensions.Options
Microsoft.Extensions.Logging.Abstractions --> Microsoft.Extensions.DependencyInjection.Abstractions
Microsoft.Extensions.Logging.Abstractions --> System.Diagnostics.DiagnosticSource
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.Configuration
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.Configuration.Abstractions
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.Configuration.Binder
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.DependencyInjection.Abstractions
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.Logging
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.Logging.Abstractions
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.Options
Microsoft.Extensions.Logging.Configuration --> Microsoft.Extensions.Options.ConfigurationExtensions
Microsoft.Extensions.Logging.Console{{Microsoft.Extensions.Logging.Console}} --> Microsoft.Extensions.DependencyInjection.Abstractions
Microsoft.Extensions.Logging.Console{{Microsoft.Extensions.Logging.Console}} --> Microsoft.Extensions.Logging
Microsoft.Extensions.Logging.Console{{Microsoft.Extensions.Logging.Console}} --> Microsoft.Extensions.Logging.Abstractions
Microsoft.Extensions.Logging.Console{{Microsoft.Extensions.Logging.Console}} --> Microsoft.Extensions.Logging.Configuration
Microsoft.Extensions.Logging.Console{{Microsoft.Extensions.Logging.Console}} --> Microsoft.Extensions.Options
Microsoft.Extensions.Logging.Console{{Microsoft.Extensions.Logging.Console}} --> System.Text.Json
Microsoft.Extensions.Options --> Microsoft.Extensions.DependencyInjection.Abstractions
Microsoft.Extensions.Options --> Microsoft.Extensions.Primitives
Microsoft.Extensions.Options.ConfigurationExtensions --> Microsoft.Extensions.Configuration.Abstractions
Microsoft.Extensions.Options.ConfigurationExtensions --> Microsoft.Extensions.Configuration.Binder
Microsoft.Extensions.Options.ConfigurationExtensions --> Microsoft.Extensions.DependencyInjection.Abstractions
Microsoft.Extensions.Options.ConfigurationExtensions --> Microsoft.Extensions.Options
Microsoft.Extensions.Options.ConfigurationExtensions --> Microsoft.Extensions.Primitives
System.Text.Json --> System.IO.Pipelines
System.Text.Json --> System.Text.Encodings.Web

class Microsoft.Extensions.Configuration default
click Microsoft.Extensions.Configuration "https://www.nuget.org/packages/Microsoft.Extensions.Configuration/9.0.6" "Microsoft.Extensions.Configuration 9.0.6"
class Microsoft.Extensions.Configuration.Abstractions default
click Microsoft.Extensions.Configuration.Abstractions "https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions/9.0.6" "Microsoft.Extensions.Configuration.Abstractions 9.0.6"
class Microsoft.Extensions.Configuration.Binder default
click Microsoft.Extensions.Configuration.Binder "https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Binder/9.0.6" "Microsoft.Extensions.Configuration.Binder 9.0.6"
class Microsoft.Extensions.DependencyInjection default
click Microsoft.Extensions.DependencyInjection "https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/9.0.6" "Microsoft.Extensions.DependencyInjection 9.0.6"
class Microsoft.Extensions.DependencyInjection.Abstractions default
click Microsoft.Extensions.DependencyInjection.Abstractions "https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions/9.0.6" "Microsoft.Extensions.DependencyInjection.Abstractions 9.0.6"
class Microsoft.Extensions.Logging default
click Microsoft.Extensions.Logging "https://www.nuget.org/packages/Microsoft.Extensions.Logging/9.0.6" "Microsoft.Extensions.Logging 9.0.6"
class Microsoft.Extensions.Logging.Abstractions default
click Microsoft.Extensions.Logging.Abstractions "https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/9.0.6" "Microsoft.Extensions.Logging.Abstractions 9.0.6"
class Microsoft.Extensions.Logging.Configuration default
click Microsoft.Extensions.Logging.Configuration "https://www.nuget.org/packages/Microsoft.Extensions.Logging.Configuration/9.0.6" "Microsoft.Extensions.Logging.Configuration 9.0.6"
class Microsoft.Extensions.Logging.Console root
class Microsoft.Extensions.Logging.Console default
click Microsoft.Extensions.Logging.Console "https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/9.0.6" "Microsoft.Extensions.Logging.Console 9.0.6"
class Microsoft.Extensions.Options default
click Microsoft.Extensions.Options "https://www.nuget.org/packages/Microsoft.Extensions.Options/9.0.6" "Microsoft.Extensions.Options 9.0.6"
class Microsoft.Extensions.Options.ConfigurationExtensions default
click Microsoft.Extensions.Options.ConfigurationExtensions "https://www.nuget.org/packages/Microsoft.Extensions.Options.ConfigurationExtensions/9.0.6" "Microsoft.Extensions.Options.ConfigurationExtensions 9.0.6"
class Microsoft.Extensions.Primitives default
click Microsoft.Extensions.Primitives "https://www.nuget.org/packages/Microsoft.Extensions.Primitives/9.0.6" "Microsoft.Extensions.Primitives 9.0.6"
class System.Diagnostics.DiagnosticSource default
click System.Diagnostics.DiagnosticSource "https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/9.0.6" "System.Diagnostics.DiagnosticSource 9.0.6"
class System.IO.Pipelines default
click System.IO.Pipelines "https://www.nuget.org/packages/System.IO.Pipelines/9.0.6" "System.IO.Pipelines 9.0.6"
class System.Text.Encodings.Web default
click System.Text.Encodings.Web "https://www.nuget.org/packages/System.Text.Encodings.Web/9.0.6" "System.Text.Encodings.Web 9.0.6"
class System.Text.Json default
click System.Text.Json "https://www.nuget.org/packages/System.Text.Json/9.0.6" "System.Text.Json 9.0.6"
```

## Installation

```sh
dotnet tool install --global nugraph
```

> [!TIP]
> On macOS, the first time a .NET global tool is installed, the PATH environment variable [must be adjusted](https://github.com/dotnet/sdk/issues/9415#issuecomment-406915716).

## Usage

In its simplest form, the `nugraph` command creates the dependency graph of the project in the current working directory and opens the resulting graph the default browser, leveraging the [Mermaid Live Editor](https://mermaid.live).