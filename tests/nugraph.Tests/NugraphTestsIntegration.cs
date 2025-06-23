namespace nugraph.Tests;

[ClassDataSource<NugraphGlobalTool>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class NugraphTestsIntegration(NugraphGlobalTool nugraph) : NugraphTests(nugraph);