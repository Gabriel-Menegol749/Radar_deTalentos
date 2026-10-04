using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Entities;

namespace RadarTalentos.API.Data;

public static class DbSeeder
{
    // Vocabulários iniciais herdados do protótipo.
    private static readonly Dictionary<string, string[]> DefaultOptions = new()
    {
        [OptionCategories.Diversity] = ["Preto", "Pardo", "Branco", "Indígena", "PCD", "Bissexual", "Homossexual"],
        [OptionCategories.Restriction] = ["Remuneração", "Localização/Mobilidade", "Disponibilidade de viagem", "Senioridade", "Comportamental", "Documentação/Vínculo"],
        [OptionCategories.Source] = ["Hunting", "Gupy", "Indicação", "Inmail", "Headhunter", "Outro"],
        [OptionCategories.RejectionReason] =
        [
            "Remuneração acima do mercado",
            "Remuneração abaixo do mercado",
            "Remuneração compatível",
            "Não aceite",
            "Senioridade",
            "Perfil comportamental",
        ],
    };

    public static async Task SeedAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        if (!await db.Options.AnyAsync())
        {
            foreach (var (category, values) in DefaultOptions)
                for (var i = 0; i < values.Length; i++)
                    db.Options.Add(new OptionItem { Category = category, Value = values[i], SortOrder = i });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync())
        {
            var email = config["Seed:AdminEmail"];
            var password = config["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Nenhum usuário cadastrado e Seed:AdminEmail/Seed:AdminPassword não configurados.");
                return;
            }
            db.Users.Add(new User
            {
                Name = config["Seed:AdminName"] ?? "Administrador",
                Email = email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = Roles.Admin,
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Usuário Admin inicial criado: {Email}", email);
        }
    }
}
