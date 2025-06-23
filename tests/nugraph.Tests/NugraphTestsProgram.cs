namespace nugraph.Tests;

[ClassDataSource<NugraphProgram>(Shared = SharedType.PerTestSession)]
[InheritsTests]
public sealed class NugraphTestsProgram(NugraphProgram nugraph) : NugraphTests(nugraph);