using System.Text.Json;
using ActivityMonitor.Core.Classification;
using FluentAssertions;
using Xunit;

namespace ActivityMonitor.Tests.ClassificationTests;

/// <summary>
/// Tests for <see cref="KeywordExtractor"/>.
/// Validates keyword extraction from text including Chinese/English word
/// handling, stop-word filtering, single-letter filtering, and digit filtering.
/// </summary>
public class KeywordExtractorTests
{
    [Fact]
    public void Extract_MixedChineseEnglish_ReturnsKeywordsWithoutStopWords()
    {
        // Arrange
        // Note: Chinese text must be space-separated for the whitespace-based tokenizer
        var extractor = new KeywordExtractor();
        var title = "Linux 内核 编译 参数 调优";

        // Act
        var result = extractor.Extract(title);

        // Assert — content keywords should be present
        result.Should().Contain("Linux");
        result.Should().Contain("内核");
        result.Should().Contain("编译");
        result.Should().Contain("参数");
        result.Should().Contain("调优");
    }

    [Fact]
    public void Extract_AllStopWords_ReturnsEmptyList()
    {
        // Arrange
        var extractor = new KeywordExtractor();
        var title = "的 了 是 在 就 都";

        // Act
        var result = extractor.Extract(title);

        // Assert — all Chinese stop words should be filtered out
        result.Should().BeEmpty();
    }

    [Fact]
    public void Extract_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        var extractor = new KeywordExtractor();
        var title = "";

        // Act
        var result = extractor.Extract(title);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Extract_NullInput_ReturnsEmptyList()
    {
        // Arrange
        var extractor = new KeywordExtractor();
        string? title = null;

        // Act
        var result = extractor.Extract(title);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Extract_EnglishOnly_ReturnsWords()
    {
        // Arrange
        var extractor = new KeywordExtractor();
        var title = "Hello world test";

        // Act
        var result = extractor.Extract(title);

        // Assert — none of these are English stop words
        result.Should().Contain("Hello");
        result.Should().Contain("world");
        result.Should().Contain("test");
    }

    [Fact]
    public void Extract_MaxKeywords_RespectsLimit()
    {
        // Arrange — provide many unique words, limit to 3
        var extractor = new KeywordExtractor();
        var title = "alpha beta gamma delta epsilon zeta eta";

        // Act
        var result = extractor.Extract(title, maxKeywords: 3);

        // Assert — at most 3 keywords returned
        result.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public void Extract_SingleLetterWords_FilteredOut()
    {
        // Arrange — single Latin letters are skipped
        var extractor = new KeywordExtractor();
        var title = "a b c test";

        // Act
        var result = extractor.Extract(title);

        // Assert — only "test" survives; single letters are filtered
        result.Should().ContainSingle("test");
        result.Should().NotContain("a");
        result.Should().NotContain("b");
        result.Should().NotContain("c");
    }

    [Fact]
    public void Extract_DigitsOnly_FilteredOut()
    {
        // Arrange — pure-digit tokens are skipped
        var extractor = new KeywordExtractor();
        var title = "123 456";

        // Act
        var result = extractor.Extract(title);

        // Assert — all digits filtered, result is empty
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAsJson_ValidInput_ReturnsJsonArray()
    {
        // Arrange
        var extractor = new KeywordExtractor();
        var title = "Linux 内核 编译 参数 调优";

        // Act
        var json = extractor.ExtractAsJson(title);

        // Assert — should be a valid JSON array string
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().StartWith("[");
        json.Should().EndWith("]");

        // Verify it can be deserialized back to a list
        var deserialized = JsonSerializer.Deserialize<List<string>>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Should().NotBeEmpty();
        deserialized.Should().Contain("Linux");
    }
}
