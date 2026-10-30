using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.Tests;

/// <summary>
/// Kitchen and outlet staff often know only their email address, so sign-in
/// accepts either identifier. These tests pin the contract: username matches
/// first (exact), email falls back case-insensitively, and a wrong password
/// never reveals which one was found.
/// </summary>
public class AuthLoginTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly AppDbContext Db = new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly IConfiguration Config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "auth-login-test-key-with-at-least-32-bytes"
            })
            .Build();
        public readonly AuthService Auth;
        public User Kitchen = null!;

        public Fixture()
        {
            Auth = new AuthService(Db, Config);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            f.Db.Roles.AddRange(
                new Role { Id = 1, Name = "Owner", Permissions = "[]" },
                new Role { Id = 2, Name = "StoreManager", Permissions = "[]" },
                new Role { Id = 3, Name = "KitchenStaff", Permissions = "[]" });
            f.Db.Outlets.Add(new Outlet
            {
                Id = 1,
                Name = "Krishana Outlet",
                Currency = "INR",
                Timezone = "Asia/Kolkata",
                IsOperating = true
            });
            const string password = "Kitchen!Bell2026";
            f.Kitchen = new User
            {
                Id = 7,
                Username = "kitchen",
                Email = "KITCHEN@Krishna.com", // deliberately mixed case
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = 3,
                OutletId = 1,
                IsActive = true
            };
            f.Db.Users.AddRange(f.Kitchen);
            await f.Db.SaveChangesAsync();
            return f;
        }

        /// <summary>
        /// Adds a user whose username is exactly the kitchen's email address,
        /// to prove the username lookup always wins over the email fallback.
        /// </summary>
        public async Task<User> AddUsernameCollidingWithKitchenEmailAsync()
        {
            var manager = new User
            {
                Id = 8,
                Username = "kitchen@krishna.com",
                Email = "manager@outlet.invalid",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager!Password123"),
                RoleId = 2,
                OutletId = 1,
                IsActive = true
            };
            Db.Users.Add(manager);
            await Db.SaveChangesAsync();
            return manager;
        }

        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task StaffCanSignInWithTheirUsername()
    {
        using var f = await Fixture.CreateAsync();
        var login = await f.Auth.LoginAsync(new LoginRequest
        {
            Username = "kitchen",
            Password = "Kitchen!Bell2026"
        });
        Assert.Equal(f.Kitchen.Id, login.User.Id);
        Assert.Equal("KitchenStaff", login.User.RoleName);
        Assert.Equal(1, login.User.OutletId);
    }

    [Fact]
    public async Task StaffCanSignInWithTheirEmailAddressRegardlessOfCase()
    {
        using var f = await Fixture.CreateAsync();
        foreach (var identifier in new[]
        {
            "kitchen@krishna.com",
            "KITCHEN@KRISHNA.COM",
            "Kitchen@Krishna.Com",
            "  kitchen@krishna.com  "
        })
        {
            var login = await f.Auth.LoginAsync(new LoginRequest
            {
                Username = identifier,
                Password = "Kitchen!Bell2026"
            });
            Assert.Equal(f.Kitchen.Id, login.User.Id);
            Assert.Equal("kitchen", login.User.Username, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task UsernameTakesPrecedenceWhenItAlsoLooksLikeAnEmailAddress()
    {
        using var f = await Fixture.CreateAsync();
        var manager = await f.AddUsernameCollidingWithKitchenEmailAsync();
        // The identifier is both a username (user 8) and the kitchen email
        // (user 7); the exact username must win.
        var login = await f.Auth.LoginAsync(new LoginRequest
        {
            Username = "kitchen@krishna.com",
            Password = "Manager!Password123"
        });
        Assert.Equal(manager.Id, login.User.Id);
        Assert.Equal("StoreManager", login.User.RoleName);
        // ...and the kitchen's password must NOT be accepted for it: once the
        // username matched, there is no silent fallback to the other account.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Auth.LoginAsync(new LoginRequest
            {
                Username = "kitchen@krishna.com",
                Password = "Kitchen!Bell2026"
            }));
    }

    [Fact]
    public async Task WrongPasswordFailsForEmailLoginToo()
    {
        using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Auth.LoginAsync(new LoginRequest
            {
                Username = "kitchen@krishna.com",
                Password = "not-the-password"
            }));
    }

    [Fact]
    public async Task UnknownIdentifierFailsWithoutLeakingWhetherItExisted()
    {
        using var f = await Fixture.CreateAsync();
        var unknown = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Auth.LoginAsync(new LoginRequest
            {
                Username = "nobody@example.invalid",
                Password = "whatever"
            }));
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Auth.LoginAsync(new LoginRequest
            {
                Username = "kitchen",
                Password = "whatever"
            }));
        Assert.Equal(unknown.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task DeletedOrInactiveAccountsCannotSignInByEmail()
    {
        using var f = await Fixture.CreateAsync();
        f.Kitchen.IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Auth.LoginAsync(new LoginRequest
            {
                Username = "kitchen@krishna.com",
                Password = "Kitchen!Bell2026"
            }));
    }

    [Fact]
    public async Task SignInRecordsTheLoginTimeOnTheAccount()
    {
        using var f = await Fixture.CreateAsync();
        Assert.Null(f.Kitchen.LastLoginAt);
        await f.Auth.LoginAsync(new LoginRequest
        {
            Username = "kitchen@krishna.com",
            Password = "Kitchen!Bell2026"
        });
        var stored = await f.Db.Users.AsNoTracking().FirstAsync(u => u.Id == f.Kitchen.Id);
        Assert.NotNull(stored.LastLoginAt);
    }
}
