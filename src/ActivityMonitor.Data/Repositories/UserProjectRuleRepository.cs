using ActivityMonitor.Core.Models;
using ActivityMonitor.Data.Database;
using Microsoft.Data.Sqlite;

namespace ActivityMonitor.Data.Repositories;

/// <summary>
/// 用户项目规则仓储。基于 user_project_rules 表提供完整 CRUD 操作。
/// </summary>
public class UserProjectRuleRepository
{
    private readonly SqliteContext _db;

    /// <summary>
    /// 使用指定的数据库上下文初始化。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    public UserProjectRuleRepository(SqliteContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 异步插入一条规则并返回含自增 ID 的完整实体。
    /// </summary>
    public async Task<UserProjectRule> InsertAsync(UserProjectRule rule)
    {
        const string sql = @"
            INSERT INTO user_project_rules
                (project_name, rule_type, rule_value, priority, is_active,
                 description, created_at, updated_at)
            VALUES
                (@project_name, @rule_type, @rule_value, @priority, @is_active,
                 @description, @created_at, @updated_at);

            SELECT last_insert_rowid();";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, rule);

        var result = await cmd.ExecuteScalarAsync();
        rule.Id = Convert.ToInt64(result);
        return rule;
    }

    /// <summary>
    /// 异步更新一条规则的全部字段。
    /// </summary>
    public async Task UpdateAsync(UserProjectRule rule)
    {
        const string sql = @"
            UPDATE user_project_rules SET
                project_name = @project_name,
                rule_type    = @rule_type,
                rule_value   = @rule_value,
                priority     = @priority,
                is_active    = @is_active,
                description  = @description,
                updated_at   = @updated_at
            WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, rule);
        cmd.Parameters.AddWithValue("@id", rule.Id);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步删除指定 ID 的规则。
    /// </summary>
    public async Task DeleteAsync(long id)
    {
        const string sql = "DELETE FROM user_project_rules WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步根据主键获取单条规则。
    /// </summary>
    public async Task<UserProjectRule?> GetByIdAsync(long id)
    {
        const string sql = "SELECT * FROM user_project_rules WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@id", id);

        var rules = await ReadRulesAsync(cmd);
        return rules.Count > 0 ? rules[0] : null;
    }

    /// <summary>
    /// 异步获取所有规则，按优先级降序排列。
    /// </summary>
    public async Task<List<UserProjectRule>> GetAllAsync()
    {
        const string sql = "SELECT * FROM user_project_rules ORDER BY priority DESC, id ASC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        return await ReadRulesAsync(cmd);
    }

    /// <summary>
    /// 异步获取指定项目名称匹配的所有已启用规则，按优先级降序排列。
    /// </summary>
    /// <param name="projectName">项目或进程名称。</param>
    public async Task<List<UserProjectRule>> GetActiveByProjectAsync(string projectName)
    {
        const string sql = @"
            SELECT * FROM user_project_rules
            WHERE project_name = @project_name AND is_active = 1
            ORDER BY priority DESC, id ASC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@project_name", projectName);

        return await ReadRulesAsync(cmd);
    }

    /// <summary>
    /// 异步获取指定规则类型的所有已启用规则。
    /// </summary>
    /// <param name="ruleType">规则类型（category / work_tag / exclude / rename）。</param>
    public async Task<List<UserProjectRule>> GetActiveByTypeAsync(string ruleType)
    {
        const string sql = @"
            SELECT * FROM user_project_rules
            WHERE rule_type = @rule_type AND is_active = 1
            ORDER BY priority DESC, id ASC;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@rule_type", ruleType);

        return await ReadRulesAsync(cmd);
    }

    /// <summary>
    /// 异步切换规则的启用状态。
    /// </summary>
    public async Task SetActiveAsync(long id, bool isActive)
    {
        const string sql = @"
            UPDATE user_project_rules
            SET is_active = @is_active, updated_at = @updated_at
            WHERE id = @id;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@is_active", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 异步获取指定项目的规则数量。
    /// </summary>
    public async Task<int> CountByProjectAsync(string projectName)
    {
        const string sql = "SELECT COUNT(*) FROM user_project_rules WHERE project_name = @project_name;";

        using var connection = await _db.GetConnectionAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@project_name", projectName);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// 为 UserProjectRule 绑定参数到 SQL 命令。
    /// </summary>
    private static void BindParameters(SqliteCommand cmd, UserProjectRule rule)
    {
        cmd.Parameters.AddWithValue("@project_name", rule.ProjectName);
        cmd.Parameters.AddWithValue("@rule_type", rule.RuleType);
        cmd.Parameters.AddWithValue("@rule_value", rule.RuleValue);
        cmd.Parameters.AddWithValue("@priority", rule.Priority);
        cmd.Parameters.AddWithValue("@is_active", rule.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@description", (object?)rule.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created_at", rule.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updated_at", rule.UpdatedAt.ToString("O"));
    }

    /// <summary>
    /// 从 SqliteDataReader 读取规则列表。
    /// </summary>
    private static async Task<List<UserProjectRule>> ReadRulesAsync(SqliteCommand cmd)
    {
        var rules = new List<UserProjectRule>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rules.Add(ReadRule(reader));
        }

        return rules;
    }

    /// <summary>
    /// 从当前行读取一条 UserProjectRule。
    /// </summary>
    private static UserProjectRule ReadRule(SqliteDataReader reader)
    {
        return new UserProjectRule
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            ProjectName = reader.GetString(reader.GetOrdinal("project_name")),
            RuleType = reader.GetString(reader.GetOrdinal("rule_type")),
            RuleValue = reader.GetString(reader.GetOrdinal("rule_value")),
            Priority = reader.GetInt32(reader.GetOrdinal("priority")),
            IsActive = reader.GetInt32(reader.GetOrdinal("is_active")) != 0,
            Description = reader.IsDBNull(reader.GetOrdinal("description"))
                ? null
                : reader.GetString(reader.GetOrdinal("description")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
        };
    }
}
