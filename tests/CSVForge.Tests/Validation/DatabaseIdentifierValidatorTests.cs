using CSVForge.Domain.Validation;

namespace CSVForge.Tests.Validation;

public sealed class DatabaseIdentifierValidatorTests
{
    [Theory]
    [InlineData("customers")]
    [InlineData("_workspace_imports")]
    [InlineData("Column_123")]
    public void IsValidTableName_ReturnsTrue_ForSafeIdentifiers(string value)
    {
        Assert.True(DatabaseIdentifierValidator.IsValidTableName(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("123customers")]
    [InlineData("customer-orders")]
    [InlineData("customer orders")]
    public void IsValidColumnName_ReturnsFalse_ForUnsafeIdentifiers(string value)
    {
        Assert.False(DatabaseIdentifierValidator.IsValidColumnName(value));
    }

    [Fact]
    public void EnsureValidColumnName_Throws_ForInvalidIdentifier()
    {
        Assert.Throws<ArgumentException>(() => DatabaseIdentifierValidator.EnsureValidColumnName("bad column"));
    }
}
