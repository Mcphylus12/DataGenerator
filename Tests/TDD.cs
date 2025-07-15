using DataGenerator;

namespace Tests;

public class TDD
{
    [Fact]
    public void Test()
    {
        var generator = new Generator()
            .AddRule(() => 5)
            .AddRule((TestInnerObject inner) => inner.Hello, Random.Shared.Next)
            .SetListSize((TestObject obj) => obj.TwoList, 500);

        var data = generator.Generate<TestObject>();

        Assert.Equal(5, data.Hello);
    }
}

public class TestObject
{
    public int Hello { get; set; }
    public IEnumerable<int>? HelloList { get; set; }
    public IList<int>? TwoList { get; set; }

    public TestInnerObject Inner { get; set; }
}

public class TestInnerObject
{
    public int Hello { get; set; }
}