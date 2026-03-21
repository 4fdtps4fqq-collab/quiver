using System.Text.Json;
using KiteFlow.Services.Academics.Api.Data;
using KiteFlow.Services.Academics.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace KiteFlow.Services.Academics.Api.Services;

public static class CourseLevelCatalogDefaults
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record PedagogicalTrackTemplateItem(string Id, string Title, string Focus, decimal WeightPercent);

    public static async Task<List<CourseLevelSetting>> EnsureDefaultsAsync(AcademicsDbContext dbContext, Guid schoolId)
    {
        var items = await dbContext.CourseLevelSettings
            .Where(x => x.SchoolId == schoolId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LevelValue)
            .ToListAsync();

        if (items.Count > 0)
        {
            return items;
        }

        var defaults = GetDefaultDefinitions();
        var changed = false;

        foreach (var definition in defaults)
        {
            if (items.Any(x => x.LevelValue == definition.LevelValue))
            {
                continue;
            }

            var setting = new CourseLevelSetting
            {
                SchoolId = schoolId,
                LevelValue = definition.LevelValue,
                Name = definition.Name,
                SortOrder = definition.SortOrder,
                IsActive = true,
                PedagogicalTrackJson = SerializeTrack(definition.Track)
            };

            dbContext.CourseLevelSettings.Add(setting);
            items.Add(setting);
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }

        return items
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LevelValue)
            .ToList();
    }

    public static IReadOnlyList<PedagogicalTrackTemplateItem> DeserializeTrack(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PedagogicalTrackTemplateItem>>(json, JsonOptions)
                ?.Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string SerializeTrack(IEnumerable<PedagogicalTrackTemplateItem> items)
        => JsonSerializer.Serialize(items, JsonOptions);

    public static object[] BuildPedagogicalTrack(CourseLevelSetting? setting, int totalMinutes)
    {
        var totalHours = Math.Max(1, (int)Math.Ceiling(totalMinutes / 60m));
        var track = setting is null
            ? GetDefaultDefinitions().Select(x => x.Track).First()
            : DeserializeTrack(setting.PedagogicalTrackJson);

        return track
            .Select((module, index) => new
            {
                id = $"{setting?.LevelValue ?? 0}-{index + 1}",
                title = module.Title,
                focus = module.Focus,
                estimatedHours = Math.Max(
                    1,
                    (int)Math.Round(totalHours * (module.WeightPercent / 100m), MidpointRounding.AwayFromZero))
            })
            .ToArray<object>();
    }

    public static string TranslateLevelName(int levelValue)
    {
        var definition = GetDefaultDefinitions().FirstOrDefault(x => x.LevelValue == levelValue);
        return string.IsNullOrWhiteSpace(definition.Name) ? $"Nível {levelValue}" : definition.Name;
    }

    public static IReadOnlyList<(int LevelValue, string Name, int SortOrder)> GetLevelCatalogOptions()
        => GetDefaultDefinitions()
            .Select(x => (x.LevelValue, x.Name, x.SortOrder))
            .ToList();

    private static IReadOnlyList<(int LevelValue, string Name, int SortOrder, IReadOnlyList<PedagogicalTrackTemplateItem> Track)> GetDefaultDefinitions()
        =>
        [
            (
                2,
                "Iniciante",
                1,
                new List<PedagogicalTrackTemplateItem>
                {
                    new("beginner-1", "Segurança e montagem", "Janela de vento, checagem de equipamento e rotina segura.", 25),
                    new("beginner-2", "Controle do kite", "Posicionamento, potência e primeiros comandos consistentes.", 25),
                    new("beginner-3", "Saída d'água", "Water start, postura e estabilização inicial.", 30),
                    new("beginner-4", "Primeiros bordos", "Deslocamento curto, retorno assistido e leitura básica.", 20)
                }
            ),
            (
                3,
                "Intermediário",
                2,
                new List<PedagogicalTrackTemplateItem>
                {
                    new("intermediate-1", "Bordos consistentes", "Controle de direção, ritmo e sustentação de navegação.", 25),
                    new("intermediate-2", "Upwind e transições", "Ganhar terreno e trocar de direção com consistência.", 30),
                    new("intermediate-3", "Autonomia supervisionada", "Escolha de material e leitura de condição.", 25),
                    new("intermediate-4", "Procedimentos de segurança", "Autorresgate e tomada de decisão.", 20)
                }
            ),
            (
                4,
                "Avançado",
                3,
                new List<PedagogicalTrackTemplateItem>
                {
                    new("advanced-1", "Performance técnica", "Ajustes finos de postura, bordos e eficiência.", 25),
                    new("advanced-2", "Manobras", "Transições dinâmicas, carving e repertório avançado.", 30),
                    new("advanced-3", "Saltos e controle aéreo", "Tempo, envio e segurança em progressão avançada.", 25),
                    new("advanced-4", "Autonomia total", "Gestão independente de sessão e leitura avançada.", 20)
                }
            )
        ];
}
