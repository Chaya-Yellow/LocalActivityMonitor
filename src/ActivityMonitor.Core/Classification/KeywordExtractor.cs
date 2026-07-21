using System.Text.Json;
using System.Text.RegularExpressions;

namespace ActivityMonitor.Core.Classification;

/// <summary>
/// 关键词提取器。
/// 从窗口标题或文本中去除停用词（中英文）、标点符号，提取有意义的词组。
/// </summary>
public partial class KeywordExtractor
{
    // ── 中文停用词（常见虚词、代词、介词、助词）─────────────
    private static readonly HashSet<string> ChineseStopWords = new(StringComparer.Ordinal)
    {
        "的", "了", "是", "在", "和", "有", "我", "不", "人", "这",
        "中", "大", "小", "上", "下", "来", "去", "就", "也", "还",
        "而", "且", "或", "但", "对", "等", "与", "从", "以", "之",
        "到", "被", "把", "着", "过", "没", "能", "会", "要", "可",
        "已", "都", "只", "又", "再", "很", "太", "更", "最", "些",
        "那", "哪", "什", "么", "怎", "为", "因", "所", "当", "让",
        "给", "向", "同", "比", "将", "使", "用", "做", "想", "看",
        "说", "问", "知", "道", "见", "听", "吃", "走", "跑", "坐",
        "站", "写", "读", "学", "叫", "个", "多", "少", "几", "每",
        "各", "某", "另", "该", "本", "此", "何", "谁", "什么", "怎么",
        "多少", "几个", "一些", "一点", "很多", "许多", "部分",
        "这个", "那个", "这些", "那些", "这里", "那里", "这边", "那边",
        "这样", "那样", "这么", "那么", "因为", "所以", "但是", "然而",
        "虽然", "不过", "如果", "即使", "由于", "为了", "除了", "关于",
        "对于", "通过", "根据", "按照", "经过", "作为", "以及", "及其",
        "而是", "还是", "或是", "或是", "并非", "就是", "并且", "或者",
        "可以", "应该", "需要", "必须", "能够", "可能", "已经", "正在",
        "将要", "一直", "一起", "不断", "重新", "仍然", "依然", "始终",
        "常常", "经常", "往往", "通常", "有时", "偶尔", "正在", "已经",
        "以前", "以后", "之前", "之后", "同时", "然后", "接着", "最后",
        "开始", "结束", "完成", "继续", "持续",
        "啊", "吧", "吗", "呢", "呀", "哦", "嗯", "哈", "呵",
        "唉", "哟", "嘛", "喔", "嗨", "喂",
    };

    // ── 英文停用词 ────────────────────────────────────────────
    private static readonly HashSet<string> EnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // 冠词/代词
        "a", "an", "the", "this", "that", "these", "those",
        "it", "its", "itself",
        "i", "me", "my", "myself", "we", "us", "our", "ours",
        "you", "your", "yours", "yourself",
        "he", "him", "his", "himself",
        "she", "her", "hers", "herself",
        "they", "them", "their", "theirs", "themselves",
        // 系动词/助动词
        "is", "are", "am", "was", "were", "be", "been", "being",
        "have", "has", "had", "having",
        "do", "does", "did", "doing",
        "will", "would", "shall", "should",
        "can", "could", "may", "might", "must", "need", "ought",
        "dare", "used",
        // 介词/连词
        "in", "on", "at", "to", "by", "for", "of", "with", "about",
        "above", "across", "after", "against", "along", "among",
        "around", "before", "behind", "below", "beneath", "beside",
        "between", "beyond", "but", "down", "during", "except",
        "from", "inside", "into", "like", "near", "off", "onto",
        "out", "outside", "over", "per", "since", "than", "through",
        "throughout", "till", "toward", "under", "underneath",
        "until", "up", "upon", "via", "within", "without",
        "and", "or", "not", "nor", "so", "if", "else",
        "because", "although", "though", "while", "whereas",
        "whether", "either", "neither", "both", "each", "every",
        "all", "any", "few", "many", "much", "more", "most",
        "some", "other", "another", "such", "no", "none", "nothing",
        // 常见副词
        "also", "only", "just", "very", "too", "really", "quite",
        "almost", "always", "never", "often", "sometimes", "usually",
        "already", "still", "yet", "even", "again", "then", "now",
        "here", "there", "everywhere", "somewhere", "anywhere",
        "well", "badly", "hard", "easily", "slowly", "quickly",
        "how", "why", "what", "when", "where", "which", "who", "whom",
        "whose", "whatever", "whenever", "wherever", "however",
        // 量词/其他高频词
        "one", "two", "first", "second", "last",
        "next", "previous", "previous", "same", "different",
        "new", "old", "good", "bad", "best", "worst",
        "get", "got", "getting", "make", "made", "making",
        "take", "took", "taken", "taking", "use", "used", "using",
        "say", "said", "saying", "tell", "told", "telling",
        "let", "lets", "letting", "go", "went", "gone", "going",
        "come", "came", "coming", "see", "saw", "seen", "seeing",
        "know", "knew", "known", "knowing", "think", "thought",
        "want", "wanted", "wanting", "give", "gave", "given", "giving",
        "find", "found", "finding", "look", "looked", "looking",
        "am", "pm", "vs",
    };

    // ── 标点和分隔符正则 ──────────────────────────────────────
    [GeneratedRegex(@"[^\p{L}\p{N}\s\-]", RegexOptions.Compiled)]
    private static partial Regex PunctuationPattern();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespacePattern();

    /// <summary>
    /// 从文本中提取关键词列表。
    /// </summary>
    /// <param name="text">输入文本（如窗口标题、页面标题）。</param>
    /// <param name="maxKeywords">最大关键词数量（默认 10）。</param>
    /// <returns>去重后的关键词列表。</returns>
    public List<string> Extract(string? text, int maxKeywords = 10)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>(0);
        }

        // Step 1: 去除标点符号，保留字母、数字、空格、连字符
        var cleaned = PunctuationPattern().Replace(text, " ");

        // Step 2: 按空白分割为单词
        var words = WhitespacePattern().Split(cleaned);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            var trimmed = word.Trim('-', '_').Trim();

            if (trimmed.Length < 1)
            {
                continue;
            }

            // 过滤纯数字
            if (trimmed.All(c => char.IsDigit(c)))
            {
                continue;
            }

            // 过滤停用词
            if (IsStopWord(trimmed))
            {
                continue;
            }

            // 过滤单个字符（除非是英文/中文字符以外的符号）
            if (trimmed.Length == 1 && char.IsLetter(trimmed[0]))
            {
                continue;
            }

            result.Add(trimmed);

            if (result.Count >= maxKeywords)
            {
                break;
            }
        }

        return result.ToList();
    }

    /// <summary>
    /// 从文本中提取关键词并以 JSON 数组字符串返回。
    /// 便于直接存入 <see cref="Models.ActivityEvent.Keywords"/>。
    /// </summary>
    public string ExtractAsJson(string? text, int maxKeywords = 10)
    {
        var keywords = Extract(text, maxKeywords);
        return JsonSerializer.Serialize(keywords);
    }

    /// <summary>
    /// 判断是否为停用词。
    /// </summary>
    private static bool IsStopWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return true;
        }

        // 检测字符串是否包含中文字符
        var hasChinese = word.Any(c => c >= 0x4E00 && c <= 0x9FFF);

        if (hasChinese)
        {
            return ChineseStopWords.Contains(word);
        }

        return EnglishStopWords.Contains(word);
    }
}
