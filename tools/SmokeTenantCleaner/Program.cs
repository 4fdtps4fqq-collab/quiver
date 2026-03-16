using Npgsql;

var options = CleanerOptions.Parse(args);
var cleaner = new SmokeTenantCleaner(options);
await cleaner.RunAsync();

internal sealed class CleanerOptions
{
    public bool Execute { get; init; }

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5432;

    public string Username { get; init; } = "postgres";

    public string Password { get; init; } = "postgres";

    public IReadOnlyList<string> Prefixes { get; init; } = new[] { "smoke-", "invite-" };

    public static CleanerOptions Parse(string[] args)
    {
        var execute = false;
        var host = "localhost";
        var port = 5432;
        var username = "postgres";
        var password = "postgres";
        var prefixes = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--execute":
                    execute = true;
                    break;
                case "--host":
                    host = ReadValue(args, ref index, "--host");
                    break;
                case "--port":
                    port = int.Parse(ReadValue(args, ref index, "--port"));
                    break;
                case "--username":
                    username = ReadValue(args, ref index, "--username");
                    break;
                case "--password":
                    password = ReadValue(args, ref index, "--password");
                    break;
                case "--prefix":
                    prefixes.Add(ReadValue(args, ref index, "--prefix"));
                    break;
                default:
                    throw new InvalidOperationException($"Argumento nao reconhecido: {args[index]}");
            }
        }

        return new CleanerOptions
        {
            Execute = execute,
            Host = host,
            Port = port,
            Username = username,
            Password = password,
            Prefixes = prefixes.Count == 0 ? new[] { "smoke-", "invite-" } : prefixes
        };
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Faltou valor para {option}.");
        }

        index++;
        return args[index];
    }
}

internal sealed class SmokeTenantCleaner
{
    private readonly CleanerOptions _options;

    public SmokeTenantCleaner(CleanerOptions options)
    {
        _options = options;
    }

    public async Task RunAsync()
    {
        var tenants = await LoadCandidateTenantsAsync();

        if (tenants.Count == 0)
        {
            Console.WriteLine("Nenhum tenant de smoke encontrado para os prefixos informados.");
            return;
        }

        Console.WriteLine(_options.Execute
            ? "Limpando tenants de smoke..."
            : "Dry-run: tenants de smoke encontrados para limpeza.");

        foreach (var tenant in tenants)
        {
            Console.WriteLine($"- {tenant.SchoolId} | slug={tenant.Slug} | display={tenant.DisplayName}");
        }

        if (!_options.Execute)
        {
            Console.WriteLine("Nada foi removido. Execute com --execute para aplicar a limpeza.");
            return;
        }

        foreach (var tenant in tenants)
        {
            await DeleteTenantAsync(tenant);
        }

        Console.WriteLine("Limpeza concluida.");
    }

    private async Task<List<CandidateTenant>> LoadCandidateTenantsAsync()
    {
        const string sql = """
SELECT "Id", "Slug", "DisplayName"
FROM schools
WHERE
""";

        var predicates = _options.Prefixes
            .Select((_, index) => $"\"Slug\" ILIKE @prefix{index}")
            .ToArray();

        await using var connection = CreateConnection("kiteflow_schools");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql + string.Join(" OR ", predicates);

        for (var index = 0; index < _options.Prefixes.Count; index++)
        {
            command.Parameters.AddWithValue($"prefix{index}", $"{_options.Prefixes[index]}%");
        }

        var tenants = new List<CandidateTenant>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tenants.Add(new CandidateTenant(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return tenants;
    }

    private async Task DeleteTenantAsync(CandidateTenant tenant)
    {
        await ExecuteNonQueryAsync("kiteflow_finance", """
DELETE FROM revenue_entries WHERE "SchoolId" = @schoolId;
DELETE FROM expense_entries WHERE "SchoolId" = @schoolId;
""", tenant.SchoolId);

        await ExecuteNonQueryAsync("kiteflow_equipment", """
DELETE FROM equipment_usage_logs WHERE "SchoolId" = @schoolId;
DELETE FROM lesson_equipment_checkout_items WHERE "SchoolId" = @schoolId;
DELETE FROM lesson_equipment_checkouts WHERE "SchoolId" = @schoolId;
DELETE FROM maintenance_records WHERE "SchoolId" = @schoolId;
DELETE FROM maintenance_rules WHERE "SchoolId" = @schoolId;
DELETE FROM equipment_items WHERE "SchoolId" = @schoolId;
DELETE FROM gear_storages WHERE "SchoolId" = @schoolId;
""", tenant.SchoolId);

        await ExecuteNonQueryAsync("kiteflow_academics", """
DELETE FROM enrollment_balance_ledger WHERE "SchoolId" = @schoolId;
DELETE FROM lessons WHERE "SchoolId" = @schoolId;
DELETE FROM enrollments WHERE "SchoolId" = @schoolId;
DELETE FROM courses WHERE "SchoolId" = @schoolId;
DELETE FROM instructors WHERE "SchoolId" = @schoolId;
DELETE FROM students WHERE "SchoolId" = @schoolId;
""", tenant.SchoolId);

        await ExecuteNonQueryAsync("kiteflow_schools", """
DELETE FROM user_profiles WHERE "SchoolId" = @schoolId;
DELETE FROM school_settings WHERE "SchoolId" = @schoolId;
DELETE FROM schools WHERE "Id" = @schoolId;
""", tenant.SchoolId);

        await ExecuteNonQueryAsync("kiteflow_identity", """
DELETE FROM refresh_sessions WHERE "UserAccountId" IN (
    SELECT "Id" FROM user_accounts WHERE "SchoolId" = @schoolId
);
DELETE FROM user_invitations WHERE "SchoolId" = @schoolId;
DELETE FROM user_accounts WHERE "SchoolId" = @schoolId;
""", tenant.SchoolId);
    }

    private async Task ExecuteNonQueryAsync(string database, string sql, Guid schoolId)
    {
        await using var connection = CreateConnection(database);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("schoolId", schoolId);
        await command.ExecuteNonQueryAsync();
    }

    private NpgsqlConnection CreateConnection(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = _options.Host,
            Port = _options.Port,
            Username = _options.Username,
            Password = _options.Password,
            Database = database
        };

        return new NpgsqlConnection(builder.ConnectionString);
    }

    private sealed record CandidateTenant(Guid SchoolId, string Slug, string DisplayName);
}
