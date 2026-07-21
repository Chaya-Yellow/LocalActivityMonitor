using ActivityMonitor.Data.Database;
using ActivityMonitor.Data.Repositories;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.DataTests;

/// <summary>
/// SettingsRepository tests using file-based SQLite for reliable isolation.
/// Each test gets a unique temp database file.
/// </summary>
public class SettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath;

    public SettingsRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"AM_Settings_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
        catch { /* best-effort cleanup */ }
    }

    private SqliteContext CreateContext() => new(_dbPath);

    // -----------------------------------------------------------------------
    // TC-DB-007-a: Set + Get round-trip.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SetAndGetAsync_KeyValue_ReturnsStoredValue()
    {
        // Arrange
        using (var setCtx = CreateContext())
        {
            var repo = new SettingsRepository(setCtx);
            await repo.SetAsync("theme", "dark");
        }

        using var getCtx = CreateContext();
        var getRepo = new SettingsRepository(getCtx);

        // Act
        var result = await getRepo.GetAsync("theme");

        // Assert
        result.Should().Be("dark");
    }

    // -----------------------------------------------------------------------
    // Get non-existing key returns null.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetAsync_NonExistingKey_ReturnsNull()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new SettingsRepository(ctx);

        // Act
        var result = await repo.GetAsync("nonexistent_key");

        // Assert
        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Get non-existing key with default returns the default.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetAsync_NonExistingKey_ReturnsDefaultValue()
    {
        // Arrange
        using var ctx = CreateContext();
        var repo = new SettingsRepository(ctx);

        // Act
        var result = await repo.GetAsync("missing_key", "fallback_value");

        // Assert
        result.Should().Be("fallback_value");
    }

    // -----------------------------------------------------------------------
    // Set same key twice; second value overwrites.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SetAsync_ExistingKey_OverwritesValue()
    {
        // Arrange
        using (var ctx1 = CreateContext())
        {
            var repo = new SettingsRepository(ctx1);
            await repo.SetAsync("overwrite_key", "initial_value");
        }

        using (var ctx2 = CreateContext())
        {
            var repo = new SettingsRepository(ctx2);
            await repo.SetAsync("overwrite_key", "final_value");
        }

        using var getCtx = CreateContext();
        var getRepo = new SettingsRepository(getCtx);

        // Act
        var result = await getRepo.GetAsync("overwrite_key");

        // Assert
        result.Should().Be("final_value");
    }

    // -----------------------------------------------------------------------
    // Delete an existing key removes the entry.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task DeleteAsync_ExistingKey_RemovesEntry()
    {
        // Arrange
        using (var setCtx = CreateContext())
        {
            var repo = new SettingsRepository(setCtx);
            await repo.SetAsync("delete_me", "some_value");
        }

        using var delCtx = CreateContext();
        var delRepo = new SettingsRepository(delCtx);
        await delRepo.DeleteAsync("delete_me");

        // Assert
        using var verifyCtx = CreateContext();
        var verifyRepo = new SettingsRepository(verifyCtx);
        var result = await verifyRepo.GetAsync("delete_me");
        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // GetAll returns all entries.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetAllAsync_MultipleEntries_ReturnsAll()
    {
        // Arrange
        using (var setCtx = CreateContext())
        {
            var repo = new SettingsRepository(setCtx);
            await repo.SetAsync("custom_a", "alpha");
            await repo.SetAsync("custom_b", "beta");
            await repo.SetAsync("custom_c", "gamma");
        }

        using var getCtx = CreateContext();
        var getRepo = new SettingsRepository(getCtx);

        // Act
        var all = await getRepo.GetAllAsync();

        // Assert — 4 defaults + 3 custom
        all.Should().ContainKey("idle_threshold_minutes").WhoseValue.Should().Be("15");
        all.Should().ContainKey("retention_days").WhoseValue.Should().Be("30");
        all.Should().ContainKey("auto_start").WhoseValue.Should().Be("true");
        all.Should().ContainKey("poll_interval_ms").WhoseValue.Should().Be("2000");
        all.Should().ContainKey("custom_a").WhoseValue.Should().Be("alpha");
        all.Should().ContainKey("custom_b").WhoseValue.Should().Be("beta");
        all.Should().ContainKey("custom_c").WhoseValue.Should().Be("gamma");
        all.Should().HaveCount(7);
    }

    // -----------------------------------------------------------------------
    // Set with empty string value.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SetAsync_EmptyValue_StoresEmptyString()
    {
        // Arrange
        using (var setCtx = CreateContext())
        {
            var repo = new SettingsRepository(setCtx);
            await repo.SetAsync("empty_setting", "");
        }

        using var getCtx = CreateContext();
        var getRepo = new SettingsRepository(getCtx);

        // Act
        var result = await getRepo.GetAsync("empty_setting");

        // Assert
        result.Should().Be("");
    }

    // -----------------------------------------------------------------------
    // Write 10 entries, then verify all present.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task ConcurrentSetAndGet_MultipleKeys_AllValuesCorrect()
    {
        // Arrange
        for (int i = 1; i <= 10; i++)
        {
            using var setCtx = CreateContext();
            var repo = new SettingsRepository(setCtx);
            await repo.SetAsync($"batch_key_{i:D2}", $"batch_value_{i:D2}");
        }

        using var getCtx = CreateContext();
        var getRepo = new SettingsRepository(getCtx);

        // Act
        var all = await getRepo.GetAllAsync();

        // Assert — 4 defaults + 10 batch = 14
        all.Should().HaveCount(14);
        for (int i = 1; i <= 10; i++)
        {
            all.Should().ContainKey($"batch_key_{i:D2}")
                .WhoseValue.Should().Be($"batch_value_{i:D2}");
        }
    }
}
