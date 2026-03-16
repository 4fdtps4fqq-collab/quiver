using Npgsql;

var options = ToolOptions.Parse(args);
var tool = new StudentPortalAccessTool(options);
await tool.RunAsync();

internal sealed record ToolOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5432;
    public string Username { get; init; } = "postgres";
    public string Password { get; init; } = "postgres";
    public string StudentName { get; init; } = "Pedro";
    public string PasswordToSet { get; init; } = "1234";
    public bool AssignEmailIfMissing { get; init; } = true;

    public static ToolOptions Parse(string[] args)
    {
        var options = new ToolOptions();

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--host":
                    options = options with { Host = ReadValue(args, ref index, "--host") };
                    break;
                case "--port":
                    options = options with { Port = int.Parse(ReadValue(args, ref index, "--port")) };
                    break;
                case "--username":
                    options = options with { Username = ReadValue(args, ref index, "--username") };
                    break;
                case "--password":
                    options = options with { Password = ReadValue(args, ref index, "--password") };
                    break;
                case "--student-name":
                    options = options with { StudentName = ReadValue(args, ref index, "--student-name") };
                    break;
                case "--set-password":
                    options = options with { PasswordToSet = ReadValue(args, ref index, "--set-password") };
                    break;
                case "--no-assign-email":
                    options = options with { AssignEmailIfMissing = false };
                    break;
                default:
                    throw new InvalidOperationException($"Argumento nao reconhecido: {args[index]}");
            }
        }

        return options;
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

internal sealed class StudentPortalAccessTool
{
    private readonly ToolOptions _options;

    public StudentPortalAccessTool(ToolOptions options)
    {
        _options = options;
    }

    public async Task RunAsync()
    {
        var student = await FindStudentAsync();
        if (student is null)
        {
            Console.WriteLine($"Nenhum aluno ativo encontrado com nome parecido com '{_options.StudentName}'.");
            return;
        }

        var email = student.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            if (!_options.AssignEmailIfMissing)
            {
                Console.WriteLine("O aluno encontrado nao possui e-mail e a atribuicao automatica foi desabilitada.");
                return;
            }

            email = BuildFallbackEmail(student);
            await AssignStudentEmailAsync(student.Id, email);
        }

        var userId = await UpsertIdentityUserAsync(student.SchoolId, email);
        await LinkStudentToIdentityAsync(student.Id, userId);

        Console.WriteLine("Acesso do portal do aluno preparado com sucesso.");
        Console.WriteLine($"Aluno: {student.FullName}");
        Console.WriteLine($"StudentId: {student.Id}");
        Console.WriteLine($"SchoolId: {student.SchoolId}");
        Console.WriteLine($"Login: {email}");
        Console.WriteLine($"Senha: {_options.PasswordToSet}");
        Console.WriteLine($"IdentityUserId: {userId}");
    }

    private async Task<StudentRecord?> FindStudentAsync()
    {
        const string sql = """
SELECT "Id", "SchoolId", "FullName", "Email"
FROM students
WHERE "IsActive" = TRUE
  AND "FullName" ILIKE @name
ORDER BY
  CASE WHEN lower("FullName") = lower(@exactName) THEN 0 ELSE 1 END,
  "CreatedAtUtc" DESC
LIMIT 2;
""";

        await using var connection = CreateConnection("kiteflow_academics");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("name", $"%{_options.StudentName}%");
        command.Parameters.AddWithValue("exactName", _options.StudentName);

        var students = new List<StudentRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            students.Add(new StudentRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        if (students.Count > 1)
        {
            Console.WriteLine("Mais de um aluno encontrado. Estou usando o mais recente:");
        }

        return students.FirstOrDefault();
    }

    private async Task AssignStudentEmailAsync(Guid studentId, string email)
    {
        const string sql = """
UPDATE students
SET "Email" = @email
WHERE "Id" = @studentId;
""";

        await using var connection = CreateConnection("kiteflow_academics");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("studentId", studentId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task LinkStudentToIdentityAsync(Guid studentId, Guid identityUserId)
    {
        const string sql = """
ALTER TABLE students
ADD COLUMN IF NOT EXISTS "IdentityUserId" uuid NULL;

UPDATE students
SET "IdentityUserId" = @identityUserId
WHERE "Id" = @studentId;
""";

        await using var connection = CreateConnection("kiteflow_academics");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("identityUserId", identityUserId);
        command.Parameters.AddWithValue("studentId", studentId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> UpsertIdentityUserAsync(Guid schoolId, string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(_options.PasswordToSet);

        const string lookupSql = """
SELECT "Id"
FROM user_accounts
WHERE lower("Email") = @email
LIMIT 1;
""";

        await using var connection = CreateConnection("kiteflow_identity");
        await connection.OpenAsync();

        Guid? userId = null;
        await using (var lookup = connection.CreateCommand())
        {
            lookup.CommandText = lookupSql;
            lookup.Parameters.AddWithValue("email", normalizedEmail);
            var result = await lookup.ExecuteScalarAsync();
            if (result is Guid existingId)
            {
                userId = existingId;
            }
        }

        if (userId.HasValue)
        {
            const string updateSql = """
UPDATE user_accounts
SET "SchoolId" = @schoolId,
    "PasswordHash" = @passwordHash,
    "Role" = 4,
    "IsActive" = TRUE,
    "MustChangePassword" = FALSE
WHERE "Id" = @id;
""";

            await using var update = connection.CreateCommand();
            update.CommandText = updateSql;
            update.Parameters.AddWithValue("schoolId", schoolId);
            update.Parameters.AddWithValue("passwordHash", passwordHash);
            update.Parameters.AddWithValue("id", userId.Value);
            await update.ExecuteNonQueryAsync();
            return userId.Value;
        }

        var newUserId = Guid.NewGuid();
        const string insertSql = """
INSERT INTO user_accounts (
    "Id",
    "SchoolId",
    "Email",
    "PasswordHash",
    "Role",
    "IsActive",
    "MustChangePassword",
    "CreatedAtUtc"
) VALUES (
    @id,
    @schoolId,
    @email,
    @passwordHash,
    4,
    TRUE,
    FALSE,
    @createdAtUtc
);
""";

        await using var insert = connection.CreateCommand();
        insert.CommandText = insertSql;
        insert.Parameters.AddWithValue("id", newUserId);
        insert.Parameters.AddWithValue("schoolId", schoolId);
        insert.Parameters.AddWithValue("email", normalizedEmail);
        insert.Parameters.AddWithValue("passwordHash", passwordHash);
        insert.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
        await insert.ExecuteNonQueryAsync();

        return newUserId;
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

    private static string BuildFallbackEmail(StudentRecord student)
    {
        var baseName = new string(student.FullName
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '.')
            .ToArray())
            .Trim('.');

        while (baseName.Contains("..", StringComparison.Ordinal))
        {
            baseName = baseName.Replace("..", ".", StringComparison.Ordinal);
        }

        return $"{baseName}.{student.Id.ToString("N")[..6]}@kiteflow.local";
    }

    private sealed record StudentRecord(Guid Id, Guid SchoolId, string FullName, string? Email);
}
