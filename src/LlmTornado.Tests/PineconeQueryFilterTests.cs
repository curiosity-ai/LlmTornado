using System.Reflection;
using System.Text.Json;
using LlmTornado.VectorDatabases;
using LlmTornado.VectorDatabases.Pinecone;
using Pinecone;

namespace LlmTornado.Tests;

[TestFixture]
public class PineconeQueryFilterTests
{
    [Test]
    public void BuildFilter_KeepsTheOperatorOfAnEqualityClause()
    {
        string json = Filter(TornadoWhereOperator.Equal("topic", "cats"));

        Assert.That(json, Is.EqualTo("""{"topic":{"$eq":"cats"}}"""));
    }

    [Test]
    public void BuildFilter_KeepsNestedLogicalOperators()
    {
        string json = Filter(TornadoWhereOperator.Equal("topic", "cats") & TornadoWhereOperator.GreaterThan("year", 2020));

        Assert.That(json, Is.EqualTo("""{"$and":[{"topic":{"$eq":"cats"}},{"year":{"$gt":2020}}]}"""));
    }

    [Test]
    public void BuildFilter_KeepsTheValuesOfAnInClause()
    {
        string json = Filter(TornadoWhereOperator.In("topic", "cats", "dogs"));

        Assert.That(json, Is.EqualTo("""{"topic":{"$in":["cats","dogs"]}}"""));
    }

    [Test]
    public void BuildFilter_WithoutWhere_ReturnsNull()
    {
        Assert.That(Build(null), Is.Null);
    }

    private static string Filter(TornadoWhereOperator where)
    {
        return JsonSerializer.Serialize(Plain(Build(where.ToWhere())));
    }

    private static object? Plain(object? value)
    {
        return value switch
        {
            null => null,
            MetadataValue metadataValue => Plain(metadataValue.Value),
            Metadata metadata => metadata.ToDictionary(x => x.Key, x => Plain(x.Value)),
            string text => text,
            System.Collections.IEnumerable items => items.Cast<object?>().Select(Plain).ToList(),
            _ => value
        };
    }

    private static Metadata? Build(Dictionary<string, object>? where)
    {
        MethodInfo method = typeof(PineconeCollectionClient).GetMethod("BuildFilter", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Metadata?)method.Invoke(null, [where]);
    }
}
