using System.Data;
using ActivityMonitor.Data.Database;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ActivityMonitor.Tests.DataTests;

public class SqliteContextTests
{
    [Fact]
    public async Task InitializeAsync_CreatesAllTables_WhenDatabaseIsNew()
    {
        // Arrange
        using var ctx = new SqliteContext(":memory:");

        // Act
        var conn = await ctx.GetConnectionAsync();

        // Assert - query sqlite_master for all 5 tables
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";

        var tables = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        tables.Should().Contain("activity_events");
        tables.Should().Contain("daily_summaries");
        tables.Should().Contain("weekly_summaries");
        tables.Should().Contain("monthly_summaries");
        tables.Should().Contain("settings");
    }

    [Fact]
    public async Task InitializeAsync_CreatesAllIndexes_WhenDatabaseIsNew()
    {
        // Arrange
        using var ctx = new SqliteContext(":memory:");

        // Act
        var conn = await ctx.GetConnectionAsync();

        // Assert - query sqlite_master for all 5 indexes
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND name LIKE 'idx_events_%' ORDER BY name;";

        var indexes = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        indexes.Should().Contain("idx_events_date");
        indexes.Should().Contain("idx_events_cat");
        indexes.Should().Contain("idx_events_proc");
        indexes.Should().Contain("idx_events_proj");
        indexes.Should().Contain("idx_events_domain");
        indexes.Should().HaveCount(5);
    }

    [Fact]
    public async Task InitializeAsync_InsertDefaults_WhenDatabaseIsNew()
    {
        // Arrange
        using var ctx = new SqliteContext(":memory:");

        // Act
        var conn = await ctx.GetConnectionAsync();

        // Assert - query settings table for default values
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = 'idle_threshold_minutes';";
        var idleThreshold = (await cmd.ExecuteScalarAsync()) as string;
        idleThreshold.Should().Be("15");

        cmd.CommandText = "SELECT value FROM settings WHERE key = 'retention_days';";
        var retentionDays = (await cmd.ExecuteScalarAsync()) as string;
        retentionDays.Should().Be("30");

        cmd.CommandText = "SELECT value FROM settings WHERE key = 'auto_start';";
        var autoStart = (await cmd.ExecuteScalarAsync()) as string;
        autoStart.Should().Be("true");

        cmd.CommandText = "SELECT value FROM settings WHERE key = 'poll_interval_ms';";
        var pollInterval = (await cmd.ExecuteScalarAsync()) as string;
        pollInterval.Should().Be("2000");
    }

    [Fact]
    public async Task Dispose_ClosesConnection_WhenCalled()
    {
        // Arrange
        SqliteConnection conn;
        using (var ctx = new SqliteContext(":memory:"))
        {
            conn = await ctx.GetConnectionAsync();
            conn.State.Should().Be(ConnectionState.Open);
        } // ctx.Dispose() called here

        // Assert - connection should be closed after dispose
        conn.State.Should().Be(ConnectionState.Closed);
    }
}
