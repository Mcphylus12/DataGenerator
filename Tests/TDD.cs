using DataGenerator;

namespace Tests;

public class TDD
{
    [Fact]
    public void Test()
    {
        var generator = new Generator()
            .AddRule(() => 5)
            .AddRule((TestInnerObject inner) => inner.Hello, g => Random.Shared.Next(10));

        var data = generator.Generate<TestObject>();

        Assert.Equal(5, data.Hello);
    }
}

public class TestObject
{
    public string FFFF { get; set; }
    public int Hello { get; set; }
    public IEnumerable<int>? HelloList { get; set; }
    public IList<int>? TwoList { get; set; }

    public TestInnerObject Inner { get; set; }
}

public class TestInnerObject
{
    public int Hello { get; set; }
}