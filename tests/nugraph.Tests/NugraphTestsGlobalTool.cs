namespace nugraph.Tests;

[ClassDataSource<NugraphGlobalTool>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class NugraphTestsGlobalTool(NugraphGlobalTool nugraph) : NugraphTests(nugraph);