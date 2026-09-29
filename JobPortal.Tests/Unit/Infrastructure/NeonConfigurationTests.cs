using FluentAssertions;
using JobPortal.Infrastructure.Data;
using Xunit;

namespace JobPortal.Tests.Unit.Infrastructure;

public class NeonConfigurationTests
{
    [Fact]
    public void Normalize_WhenGivenPostgresUri_ConvertsToNpgsqlAdoNetFormat()
    {
        // Arrange
        var uri = "postgresql://neondb_owner:secretPassword123@ep-example-pooler.c-7.us-east-2.aws.neon.tech/neondb?sslmode=require";

        // Act
        var normalized = ConnectionStringHelper.Normalize(uri);

        // Assert
        normalized.Should().Contain("Host=ep-example-pooler.c-7.us-east-2.aws.neon.tech");
        normalized.Should().Contain("Port=5432");
        normalized.Should().Contain("Database=neondb");
        normalized.Should().Contain("Username=neondb_owner");
        normalized.Should().Contain("Password=secretPassword123");
        normalized.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void Normalize_WhenGivenNeonHost_EnforcesSslRequire()
    {
        // Arrange
        var connectionString = "Host=ep-test-pooler.neon.tech;Port=5432;Database=neondb;Username=postgres;Password=pass;SSL Mode=Disable";

        // Act
        var normalized = ConnectionStringHelper.Normalize(connectionString);

        // Assert
        normalized.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void Normalize_WhenNullOrEmpty_ReturnsOriginal()
    {
        ConnectionStringHelper.Normalize("").Should().BeEmpty();
        ConnectionStringHelper.Normalize(null).Should().BeEmpty();
    }
}
