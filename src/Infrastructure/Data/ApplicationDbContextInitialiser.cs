using greenfield_checkout.Domain.Constants;
using greenfield_checkout.Domain.Entities;
using greenfield_checkout.Domain.Enums;
using greenfield_checkout.Domain.ValueObjects;
using greenfield_checkout.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace greenfield_checkout.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
        await initialiser.SeedAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ApplicationDbContextInitialiser(ILogger<ApplicationDbContextInitialiser> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            // See https://jasontaylor.dev/ef-core-database-initialisation-strategies
            await _context.Database.EnsureDeletedAsync();
            await _context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // Default roles
        var administratorRole = new IdentityRole(Roles.Administrator);

        if (_roleManager.Roles.All(r => r.Name != administratorRole.Name))
        {
            await _roleManager.CreateAsync(administratorRole);
        }

        // Default users
        var administrator = new ApplicationUser { UserName = "administrator@localhost", Email = "administrator@localhost" };

        if (_userManager.Users.All(u => u.UserName != administrator.UserName))
        {
            await _userManager.CreateAsync(administrator, "Administrator1!");
            if (!string.IsNullOrWhiteSpace(administratorRole.Name))
            {
                await _userManager.AddToRolesAsync(administrator, new [] { administratorRole.Name });
            }
        }

        // Default data
        // Seed, if necessary
        if (!_context.TodoLists.Any())
        {
            _context.TodoLists.Add(new TodoList
            {
                Title = "Tasks",
                Colour = Colour.Green,
                Items =
                {
                    new TodoItem { Title = "Make a todo list 📃" },
                    new TodoItem { Title = "Check off the first item ✅" },
                    new TodoItem { Title = "Realise you've already done two things on the list! 🤯"},
                    new TodoItem { Title = "Reward yourself with a nice, long nap 🏆" },
                }
            });

            await _context.SaveChangesAsync();
        }

        // SPEC-2026-0043 slice 2A: seed the promo code catalog used by Gherkin scenarios.
        if (!await _context.PromoCodes.AnyAsync())
        {
            var startOfYear = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endOfYear = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

            _context.PromoCodes.AddRange(
                new PromoCode("VERANO20", PromoCodeType.Percentage, 20m, startOfYear, endOfYear, 10_000),
                new PromoCode("FIJO50", PromoCodeType.Fixed, 50m, startOfYear, endOfYear, 10_000),
                new PromoCode("MEGA50", PromoCodeType.Percentage, 50m, startOfYear, endOfYear, 10_000, maxDiscount: 30m),
                new PromoCode("PRIMAVERA", PromoCodeType.Percentage, 10m,
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero),
                    1_000));

            var agotado = new PromoCode("AGOTADO", PromoCodeType.Percentage, 10m, startOfYear, endOfYear, 1);
            agotado.Consume();
            _context.PromoCodes.Add(agotado);

            await _context.SaveChangesAsync();
        }
    }
}
